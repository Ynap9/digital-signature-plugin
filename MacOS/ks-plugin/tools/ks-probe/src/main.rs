use std::error::Error;
use std::io::Write;
use std::path::Path;
use std::process::ExitCode;
use std::time::{Duration, Instant};

use ks_plugin_external::card::CardError;
use ks_plugin_external::card::driver::{self, Bit4idVgcaDriver, CardLayout, PinStatus, SignScheme};
use ks_plugin_external::card::pin::Pin;
use ks_plugin_external::card::pkcs15::{KeyInfo, PinInfo};
use ks_plugin_external::card::transport::{PcscReaders, PcscTransport, Transport};
use ks_plugin_external::certificate::store::CertificateStore;
use ks_plugin_external::certificate::{self, CertificateDetails, RsaPublicKey};

type ProbeResult<T = ()> = Result<T, Box<dyn Error>>;

const USAGE: &str = "Cách dùng: ks-probe <readers | atr | dump [--store <dir>] | import-cert <file> --store <dir> | pin-status | sign-test [--rounds <n>]>";
const TEST_MESSAGE: &[u8] = b"ks-plugin sign test";
const DEFAULT_ROUNDS: u32 = 20;
const SIGN_SCHEME_CANDIDATES: [SignScheme; 4] = [
    SignScheme {
        algorithm: None,
        send_digest_info: true,
    },
    SignScheme {
        algorithm: Some(0x02),
        send_digest_info: true,
    },
    SignScheme {
        algorithm: None,
        send_digest_info: false,
    },
    SignScheme {
        algorithm: Some(0x02),
        send_digest_info: false,
    },
];

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let result = match args.first().map(String::as_str) {
        Some("readers") => readers(),
        Some("atr") => atr(),
        Some("dump") => dump(option(&args, "--store")),
        Some("import-cert") => match (args.get(1), option(&args, "--store")) {
            (Some(file), Some(store)) => import_certificate(Path::new(file), store),
            _ => Err(USAGE.into()),
        },
        Some("pin-status") => pin_status(),
        Some("sign-test") => rounds(&args).and_then(sign_test),
        _ => Err(USAGE.into()),
    };

    match result {
        Ok(()) => ExitCode::SUCCESS,
        Err(error) => {
            eprintln!("Lỗi: {error}");
            ExitCode::FAILURE
        }
    }
}

fn readers() -> ProbeResult {
    let readers = PcscReaders::establish()?.list()?;
    if readers.is_empty() {
        return Err(CardError::NoReader.into());
    }
    for reader in readers {
        let atr = reader
            .atr
            .map_or_else(|| "(chưa cắm thẻ)".to_owned(), hex::encode_upper);
        println!("{}  {atr}", reader.name);
    }
    Ok(())
}

fn atr() -> ProbeResult {
    let (reader, transport) = connect_first_card()?;
    println!("Reader : {reader}");
    println!("ATR    : {}", hex::encode_upper(transport.atr()?));
    Ok(())
}

fn dump(store_dir: Option<&Path>) -> ProbeResult {
    let (reader, mut transport) = connect_first_card()?;
    let atr = transport.atr()?;
    let driver_name = if driver::matches_atr(&atr) {
        "Bit4idVgcaDriver"
    } else {
        "(chưa hỗ trợ)"
    };
    println!("Reader : {reader}");
    println!("ATR    : {} → {driver_name}", hex::encode_upper(&atr));

    let mut card = Bit4idVgcaDriver::new(transport.transaction()?);
    let layout = CardLayout::parse(&card.read_files()?)?;
    print_layout(&layout);

    for certificate in &layout.certificates {
        let content = card.read_certificate_file(&certificate.path)?;
        println!(
            "Cert   : id {} → EF {}, tag {:02X}, {} bytes",
            hex::encode_upper(&certificate.id),
            hex::encode_upper(&certificate.path),
            content.first().copied().unwrap_or_default(),
            content.len()
        );
    }

    if let Some(dir) = store_dir {
        let store = CertificateStore::new(dir);
        for key in &layout.private_keys {
            let card_key = read_card_key(&mut card, &layout, key)?;
            match store.load(&key.id)? {
                Some(der) => {
                    let details = CertificateDetails::from_der(der)?;
                    print_certificate(&certificate::verify_matches_card(details, &card_key)?);
                }
                None => println!(
                    "Nạp    : chưa có chứng thư cho khoá {}",
                    hex::encode_upper(&key.id)
                ),
            }
        }
    }

    Ok(())
}

fn import_certificate(file: &Path, store_dir: &Path) -> ProbeResult {
    let (_, mut transport) = connect_first_card()?;
    let mut card = Bit4idVgcaDriver::new(transport.transaction()?);
    let layout = CardLayout::parse(&card.read_files()?)?;
    let key = layout
        .private_keys
        .first()
        .ok_or("Token không có khoá bí mật nào")?;
    let card_key = read_card_key(&mut card, &layout, key)?;

    let details = CertificateDetails::from_file_bytes(&std::fs::read(file)?)?;
    let details = certificate::verify_matches_card(details, &card_key)?;
    CertificateStore::new(store_dir).save(&key.id, &details.der)?;
    println!("Đã nạp chứng thư cho khoá {}", hex::encode_upper(&key.id));
    print_certificate(&details);
    Ok(())
}

fn pin_status() -> ProbeResult {
    let (_, mut transport) = connect_first_card()?;
    let mut card = Bit4idVgcaDriver::new(transport.transaction()?);
    let layout = CardLayout::parse(&card.read_files()?)?;
    match card.pin_status(user_pin(&layout)?)? {
        PinStatus::Verified => println!("PIN    : đang ở trạng thái đã xác thực"),
        PinStatus::TriesLeft(tries) => println!("PIN    : còn {tries} lần thử"),
    }
    Ok(())
}

fn sign_test(rounds: u32) -> ProbeResult {
    let pin = ask_pin()?;

    let (_, mut transport) = connect_first_card()?;
    let mut card = Bit4idVgcaDriver::new(transport.transaction()?);
    let layout = CardLayout::parse(&card.read_files()?)?;
    let key = layout
        .private_keys
        .first()
        .ok_or("Token không có khoá bí mật nào")?;
    let card_key = read_card_key(&mut card, &layout, key)?;
    card.verify_pin(user_pin(&layout)?, &pin)?;
    println!("PIN    : xác thực thành công");

    let mut chosen = None;
    for scheme in SIGN_SCHEME_CANDIDATES {
        match card.sign_sha256(key, TEST_MESSAGE, scheme) {
            Ok(signature) if card_key.verify_pkcs1_sha256(TEST_MESSAGE, &signature) => {
                println!(
                    "Ký     : {scheme:?} → chữ ký {} byte, tự kiểm hợp lệ",
                    signature.len()
                );
                chosen = Some(scheme);
                break;
            }
            Ok(signature) => println!(
                "Ký     : {scheme:?} → {} byte nhưng không khớp khoá công khai",
                signature.len()
            ),
            Err(error) => println!("Ký     : {scheme:?} → {error}"),
        }
    }
    let scheme = chosen.ok_or("Không biến thể MSE/PSO nào cho ra chữ ký hợp lệ")?;

    let mut timings = Vec::with_capacity(rounds as usize);
    for _ in 0..rounds {
        let started = Instant::now();
        let signature = card.sign_sha256(key, TEST_MESSAGE, scheme)?;
        timings.push(started.elapsed());
        if !card_key.verify_pkcs1_sha256(TEST_MESSAGE, &signature) {
            return Err(CardError::InvalidSignature.into());
        }
    }
    print_timings(&timings);
    Ok(())
}

fn ask_pin() -> ProbeResult<Pin> {
    let (_, mut transport) = connect_first_card()?;
    let mut card = Bit4idVgcaDriver::new(transport.transaction()?);
    let layout = CardLayout::parse(&card.read_files()?)?;
    let pin_info = user_pin(&layout)?.clone();
    let tries = match card.pin_status(&pin_info)? {
        PinStatus::TriesLeft(tries) if tries < driver::MIN_PIN_TRIES_TO_VERIFY => {
            return Err(CardError::TooFewPinTries(tries).into());
        }
        PinStatus::TriesLeft(tries) => tries,
        PinStatus::Verified => {
            return Err(
                "Thẻ đang ở trạng thái đã xác thực — rút token ra cắm lại rồi chạy lại".into(),
            );
        }
    };
    drop(card);
    drop(transport);

    print!("Nhập PIN (token còn {tries} lần thử, chỉ gửi một lần): ");
    std::io::stdout().flush()?;
    let text = zeroize::Zeroizing::new(rpassword::read_password()?);
    Ok(Pin::new(&text, &pin_info)?)
}

fn print_timings(timings: &[Duration]) {
    let millis = |duration: &Duration| duration.as_secs_f64() * 1000.0;
    let total: f64 = timings.iter().map(millis).sum();
    let fastest = timings.iter().map(millis).fold(f64::INFINITY, f64::min);
    let slowest = timings.iter().map(millis).fold(0.0, f64::max);
    println!(
        "Tốc độ : {} lượt, không hỏi PIN lại · trung bình {:.1} ms · nhanh nhất {fastest:.1} ms · chậm nhất {slowest:.1} ms",
        timings.len(),
        total / timings.len().max(1) as f64
    );
}

fn user_pin(layout: &CardLayout) -> ProbeResult<&PinInfo> {
    let key = layout
        .private_keys
        .first()
        .ok_or("Token không có khoá bí mật nào")?;
    Ok(layout
        .pins
        .iter()
        .find(|pin| key.auth_id.as_deref() == Some(pin.auth_id.as_slice()))
        .ok_or("Không tìm thấy PIN bảo vệ khoá ký")?)
}

fn rounds(args: &[String]) -> ProbeResult<u32> {
    option(args, "--rounds").map_or(Ok(DEFAULT_ROUNDS), |value| {
        value.to_string_lossy().parse().map_err(Into::into)
    })
}

fn read_card_key<T: Transport>(
    card: &mut Bit4idVgcaDriver<T>,
    layout: &CardLayout,
    key: &KeyInfo,
) -> ProbeResult<RsaPublicKey> {
    let public_key = layout
        .public_keys
        .iter()
        .find(|public_key| public_key.id == key.id)
        .ok_or("Token không có khoá công khai khớp ID khoá bí mật")?;
    Ok(card.read_public_key(public_key)?)
}

fn print_layout(layout: &CardLayout) {
    let token = &layout.token_info;
    println!(
        "Token  : {} · serial {} · {}",
        token.label.as_deref().unwrap_or("-"),
        token.serial_number,
        token.manufacturer_id.as_deref().unwrap_or("-")
    );
    for pin in &layout.pins {
        println!(
            "{:<7}: ref {:#04X}, len {}..{}, stored {}, pad {}, {:?}",
            pin.label,
            pin.reference,
            pin.min_length,
            pin.max_length.unwrap_or(pin.stored_length),
            pin.stored_length,
            pin.pad_char
                .map_or_else(|| "-".to_owned(), |pad| format!("{pad:02X}")),
            pin.encoding
        );
    }
    for key in &layout.private_keys {
        println!(
            "Key    : ref {}, RSA {}, id {}, path {}, auth {}",
            key.key_reference
                .map_or_else(|| "-".to_owned(), |reference| format!("{reference:#04X}")),
            key.modulus_bits,
            hex::encode_upper(&key.id),
            hex::encode_upper(&key.path),
            key.auth_id.as_deref().map_or_else(
                || "-".to_owned(),
                |auth| String::from_utf8_lossy(auth).into_owned()
            )
        );
    }
    for key in &layout.public_keys {
        println!(
            "PubKey : id {}, path {}, {} bit",
            hex::encode_upper(&key.id),
            hex::encode_upper(&key.path),
            key.modulus_bits
        );
    }
}

fn print_certificate(details: &CertificateDetails) {
    println!("Subject: {}", details.subject);
    println!("Issuer : {}", details.issuer);
    println!(
        "Serial : {} · thumbprint {}",
        details.serial_number, details.thumbprint
    );
    println!(
        "Ký được: {} · khoá RSA {} bit",
        details.allows_signing,
        details.public_key.modulus_bits()
    );
}

fn option<'a>(args: &'a [String], flag: &str) -> Option<&'a Path> {
    args.iter()
        .position(|arg| arg == flag)
        .and_then(|index| args.get(index + 1))
        .map(Path::new)
}

fn connect_first_card() -> ProbeResult<(String, PcscTransport)> {
    let pcsc = PcscReaders::establish()?;
    let reader = pcsc
        .list()?
        .into_iter()
        .find(|reader| reader.atr.is_some())
        .ok_or(CardError::NoReader)?;
    let transport = pcsc.connect(&reader.name)?;
    Ok((reader.name, transport))
}
