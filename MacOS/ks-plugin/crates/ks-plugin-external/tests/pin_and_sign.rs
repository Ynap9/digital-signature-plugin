use std::cell::RefCell;
use std::collections::VecDeque;

use ks_plugin_external::card::driver::{Bit4idVgcaDriver, PinStatus, SignScheme};
use ks_plugin_external::card::pin::{Pin, PinPadding};
use ks_plugin_external::card::pkcs15::{self, KeyInfo, PinInfo};
use ks_plugin_external::card::transport::Transport;
use ks_plugin_external::card::{CardError, CardResult};

const AODF: &[u8] = include_bytes!("fixtures/vgca/7001.bin");
const PRKDF: &[u8] = include_bytes!("fixtures/vgca/7002.bin");
const TEST_PIN: &str = "1234";

struct ScriptedCard {
    sent: RefCell<Vec<Vec<u8>>>,
    replies: RefCell<VecDeque<Vec<u8>>>,
}

impl ScriptedCard {
    fn new(replies: &[&[u8]]) -> Self {
        Self {
            sent: RefCell::new(Vec::new()),
            replies: RefCell::new(replies.iter().map(|reply| reply.to_vec()).collect()),
        }
    }
}

impl Transport for ScriptedCard {
    fn transmit(&self, apdu: &[u8]) -> CardResult<Vec<u8>> {
        self.sent.borrow_mut().push(apdu.to_vec());
        self.replies
            .borrow_mut()
            .pop_front()
            .ok_or(CardError::NoReader)
    }
}

fn user_pin() -> CardResult<PinInfo> {
    pkcs15::parse_aodf(AODF)?
        .into_iter()
        .find(|pin| pin.label == "PIN")
        .ok_or(CardError::Malformed("fixture thiếu PIN"))
}

fn signing_key() -> CardResult<KeyInfo> {
    pkcs15::parse_key_directory(PRKDF)?
        .into_iter()
        .next()
        .ok_or(CardError::Malformed("fixture thiếu khoá"))
}

#[test]
fn token_pin_is_local_without_padding_flag() {
    let info = user_pin().unwrap();

    assert!(info.flags.local);
    assert!(!info.flags.needs_padding);
    assert_eq!((info.stored_length, info.pad_char), (16, Some(0xFF)));
}

#[test]
fn pads_pin_to_requested_length() {
    let pin = Pin::new(TEST_PIN, &user_pin().unwrap()).unwrap();

    let encoded = pin.encode(Some(PinPadding {
        length: 16,
        byte: 0xFF,
    }));

    assert_eq!(&encoded[..4], TEST_PIN.as_bytes());
    assert_eq!(&encoded[4..], [0xFF; 12]);
    assert_eq!(pin.encode(None).as_slice(), TEST_PIN.as_bytes());
}

#[test]
fn rejects_malformed_pin_before_touching_card() {
    let info = user_pin().unwrap();

    assert!(matches!(
        Pin::new("123", &info),
        Err(CardError::InvalidPinFormat)
    ));
    assert!(matches!(
        Pin::new(&"1".repeat(17), &info),
        Err(CardError::InvalidPinFormat)
    ));
    assert!(matches!(
        Pin::new("12\u{0}4", &info),
        Err(CardError::InvalidPinFormat)
    ));
    assert_eq!(
        format!("{:?}", Pin::new(TEST_PIN, &info).unwrap()),
        "Pin(***)"
    );
}

#[test]
fn queries_tries_with_local_reference_and_no_data() {
    let mut driver = Bit4idVgcaDriver::new(ScriptedCard::new(&[&[0x63, 0xC3]]));

    let status = driver.pin_status(&user_pin().unwrap()).unwrap();

    assert_eq!(status, PinStatus::TriesLeft(3));
    assert_eq!(
        driver.transport().sent.borrow()[0],
        [0x00, 0x20, 0x00, 0x83]
    );
}

#[test]
fn never_sends_pin_when_only_one_try_left() {
    let info = user_pin().unwrap();
    let mut driver = Bit4idVgcaDriver::new(ScriptedCard::new(&[&[0x63, 0xC1]]));

    let error = driver
        .verify_pin(&info, &Pin::new(TEST_PIN, &info).unwrap())
        .unwrap_err();

    assert!(matches!(error, CardError::TooFewPinTries(1)));
    assert_eq!(driver.transport().sent.borrow().len(), 1);
}

#[test]
fn skips_pin_when_card_is_already_verified() {
    let info = user_pin().unwrap();
    let mut driver = Bit4idVgcaDriver::new(ScriptedCard::new(&[&[0x90, 0x00]]));

    driver
        .verify_pin(&info, &Pin::new(TEST_PIN, &info).unwrap())
        .unwrap();

    assert_eq!(driver.transport().sent.borrow().len(), 1);
}

#[test]
fn sends_pin_once_and_reports_remaining_tries_on_mismatch() {
    let info = user_pin().unwrap();
    let mut driver = Bit4idVgcaDriver::new(ScriptedCard::new(&[&[0x63, 0xC3], &[0x63, 0xC2]]));

    let error = driver
        .verify_pin(&info, &Pin::new(TEST_PIN, &info).unwrap())
        .unwrap_err();

    assert!(matches!(error, CardError::WrongPin(2)));
    let sent = driver.transport().sent.borrow();
    assert_eq!(sent.len(), 2);
    let mut expected = vec![0x00, 0x20, 0x00, 0x83, 0x10, b'1', b'2', b'3', b'4'];
    expected.extend([0xFF; 12]);
    assert_eq!(sent[1], expected);
}

#[test]
fn signs_sha256_digest_info_with_key_reference() {
    let mut driver = Bit4idVgcaDriver::new(ScriptedCard::new(&[
        &[0x90, 0x00],
        &[0xAB, 0xCD, 0x90, 0x00],
    ]));
    let scheme = SignScheme {
        algorithm: None,
        send_digest_info: true,
    };

    let signature = driver
        .sign_sha256(&signing_key().unwrap(), b"abc", scheme)
        .unwrap();

    assert_eq!(signature, [0xAB, 0xCD]);
    let sent = driver.transport().sent.borrow();
    assert_eq!(sent[0], [0x00, 0x22, 0x41, 0xB6, 0x03, 0x84, 0x01, 0x10]);
    assert_eq!(&sent[1][..5], [0x00, 0x2A, 0x9E, 0x9A, 51]);
    assert_eq!(
        &sent[1][5..24],
        [
            0x30, 0x31, 0x30, 0x0D, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02,
            0x01, 0x05, 0x00, 0x04, 0x20
        ]
    );
    assert_eq!(&sent[1][24..28], [0xBA, 0x78, 0x16, 0xBF]);
}
