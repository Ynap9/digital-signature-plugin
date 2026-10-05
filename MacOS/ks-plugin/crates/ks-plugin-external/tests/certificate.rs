use std::time::{Duration, SystemTime, UNIX_EPOCH};

use chrono::FixedOffset;
use ks_plugin_external::card::CardResult;
use ks_plugin_external::card::driver::Bit4idVgcaDriver;
use ks_plugin_external::card::pkcs15::KeyInfo;
use ks_plugin_external::card::transport::Transport;
use ks_plugin_external::certificate::sign_cert::card_certificate_dto;
use ks_plugin_external::certificate::store::CertificateStore;
use ks_plugin_external::certificate::{self, CertificateDetails, CertificateError};

const CPG2_PEM: &[u8] = include_bytes!("fixtures/ca/cpg2.crt");
const ROOTCAG2_PEM: &[u8] = include_bytes!("fixtures/ca/rootcag2.crt");
const CARD_PUBLIC_KEY: &[u8] = include_bytes!("fixtures/vgca/DF10.bin");

struct FileCard {
    file: &'static [u8],
}

impl Transport for FileCard {
    fn transmit(&self, apdu: &[u8]) -> CardResult<Vec<u8>> {
        let mut reply = match apdu {
            [_, 0xA4, ..] => Vec::new(),
            [_, 0xB0, high, low, le] => {
                let start = usize::from(u16::from_be_bytes([*high, *low]));
                let end =
                    (start + if *le == 0 { 256 } else { usize::from(*le) }).min(self.file.len());
                self.file[start..end].to_vec()
            }
            _ => unreachable!("lệnh ngoài kịch bản"),
        };
        reply.extend_from_slice(&[0x90, 0x00]);
        Ok(reply)
    }
}

fn unix(seconds: u64) -> SystemTime {
    UNIX_EPOCH + Duration::from_secs(seconds)
}

fn card_public_key() -> CardResult<certificate::RsaPublicKey> {
    let key = KeyInfo {
        label: String::new(),
        id: vec![0xBE, 0x72, 0xEC, 0xF5],
        auth_id: None,
        key_reference: None,
        path: vec![0xDF, 0x10],
        modulus_bits: 3072,
    };
    let mut driver = Bit4idVgcaDriver::new(FileCard {
        file: CARD_PUBLIC_KEY,
    });
    driver.read_public_key(&key)
}

#[test]
fn parses_ca_certificate_like_dotnet() {
    let details = CertificateDetails::from_file_bytes(CPG2_PEM).unwrap();

    assert_eq!(
        details.subject,
        "CN=CA phục vụ các cơ quan Nhà nước G2, O=Ban Cơ yếu Chính phủ, C=VN"
    );
    assert_eq!(
        details.issuer,
        "CN=RootCA chuyên dùng Chính phủ G2, O=Ban Cơ yếu Chính phủ, C=VN"
    );
    assert_eq!(details.common_name, "CA phục vụ các cơ quan Nhà nước G2");
    assert_eq!(
        details.issuer_common_name,
        "RootCA chuyên dùng Chính phủ G2"
    );
    assert_eq!(details.serial_number, "7AF2DF52182653D3");
    assert_eq!(
        details.thumbprint,
        "55C5D432D4AA052A65A39682308753FF1DB91F85"
    );
    assert_eq!(details.not_before, unix(1_534_925_196));
    assert_eq!(details.not_after, unix(2_481_003_399));
    assert!(!details.allows_signing);
}

#[test]
fn der_and_pem_give_same_details() {
    let from_pem = CertificateDetails::from_file_bytes(ROOTCAG2_PEM).unwrap();

    let from_der = CertificateDetails::from_file_bytes(&from_pem.der).unwrap();

    assert_eq!(from_der, from_pem);
    assert_eq!(
        from_der.thumbprint,
        "761A8D4043C70E020B8578B78F89BC16A637918B"
    );
}

#[test]
fn reads_card_public_key_from_df10() {
    let key = card_public_key().unwrap();

    assert_eq!(key.exponent, [0x01, 0x00, 0x01]);
    assert_eq!(key.modulus_bits(), 3072);
}

#[test]
fn rejects_certificate_of_another_key() {
    let details = CertificateDetails::from_file_bytes(CPG2_PEM).unwrap();

    let error = certificate::verify_matches_card(details, &card_public_key().unwrap()).unwrap_err();

    assert!(matches!(error, CertificateError::KeyMismatch));
}

#[test]
fn rejects_non_certificate_bytes() {
    let error = CertificateDetails::from_file_bytes(b"not a certificate").unwrap_err();

    assert!(matches!(error, CertificateError::InvalidEncoding));
}

#[test]
fn dto_reports_key_usage_reason_in_contract_shape() {
    let details = CertificateDetails::from_file_bytes(CPG2_PEM).unwrap();
    let vietnam = FixedOffset::east_opt(7 * 3600).unwrap();

    let dto = card_certificate_dto(
        &details,
        "PC/SC: bit4id TokenME EVO v2 0",
        unix(1_790_000_000),
        &vietnam,
    );
    let json = serde_json::to_value(&dto).unwrap();

    assert_eq!(json["validFrom"], "22/08/2018 15:06:36");
    assert_eq!(json["source"], 2);
    assert_eq!(json["isExpired"], false);
    assert_eq!(json["hasPrivateKey"], true);
    assert_eq!(
        json["reason"],
        "Chứng thư số không được cấp quyền ký (KeyUsage thiếu digitalSignature/nonRepudiation)."
    );
}

#[test]
fn dto_reports_expiry_before_key_usage() {
    let details = CertificateDetails::from_file_bytes(CPG2_PEM).unwrap();
    let vietnam = FixedOffset::east_opt(7 * 3600).unwrap();

    let dto = card_certificate_dto(&details, "", unix(2_500_000_000), &vietnam);

    assert!(dto.is_expired);
    assert_eq!(
        dto.reason.as_deref(),
        Some("Chứng thư số ngoài thời hạn hiệu lực (22/08/2018 15:06:36 - 14/08/2048 14:36:39).")
    );
}

#[test]
fn store_round_trips_by_key_id() {
    let dir = std::env::temp_dir().join(format!("ks-plugin-store-{}", std::process::id()));
    let store = CertificateStore::new(&dir);

    assert_eq!(store.load(&[0xBE, 0x72]).unwrap(), None);
    store.save(&[0xBE, 0x72], b"der").unwrap();
    assert_eq!(
        store.load(&[0xBE, 0x72]).unwrap().as_deref(),
        Some(b"der".as_slice())
    );

    std::fs::remove_dir_all(dir).unwrap();
}
