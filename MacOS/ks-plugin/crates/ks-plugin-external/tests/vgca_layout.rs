use ks_plugin_external::card::driver::{self, CardFiles, CardLayout, VGCA_APPLET_AID};
use ks_plugin_external::card::pkcs15::{self, DirectoryKind, PinEncoding};

macro_rules! fixture {
    ($name:literal) => {
        include_bytes!(concat!("fixtures/vgca/", $name, ".bin")).to_vec()
    };
}

const KEY_ID: [u8; 4] = [0xBE, 0x72, 0xEC, 0xF5];
const VGCA_ATR: [u8; 25] = [
    0x3B, 0xFF, 0x18, 0x00, 0x00, 0x81, 0x31, 0xFE, 0x45, 0x00, 0x6B, 0x15, 0x0C, 0x03, 0x02, 0x01,
    0x01, 0x01, 0x42, 0x34, 0x44, 0x10, 0x31, 0x80, 0x0D,
];

fn files() -> CardFiles {
    CardFiles {
        dir: fixture!("2F00"),
        odf: fixture!("5031"),
        token_info: fixture!("5032"),
        aodf: fixture!("7001"),
        prkdf: fixture!("7002"),
        pukdf: fixture!("7004"),
        cdf: fixture!("7005"),
    }
}

#[test]
fn recognises_vgca_atr() {
    assert!(driver::matches_atr(&VGCA_ATR));
    assert!(!driver::matches_atr(&[0x3B, 0x8F, 0x80, 0x01]));
}

#[test]
fn dir_points_to_odf_and_token_info() {
    let application = &pkcs15::parse_dir(&fixture!("2F00")).unwrap()[0];

    assert_eq!(application.aid, VGCA_APPLET_AID);
    assert_eq!(application.label, "JCOP4Bit4ID");
    assert_eq!(
        application.odf_path.as_deref(),
        Some([0x50, 0x31].as_slice())
    );
    assert_eq!(
        application.token_info_path.as_deref(),
        Some([0x50, 0x32].as_slice())
    );
}

#[test]
fn odf_lists_directories() {
    let entries = pkcs15::parse_odf(&fixture!("5031")).unwrap();
    let path = |kind| {
        entries
            .iter()
            .find(|entry| entry.kind == kind)
            .map(|entry| entry.path.clone())
    };

    assert_eq!(path(DirectoryKind::AuthObjects), Some(vec![0x70, 0x01]));
    assert_eq!(path(DirectoryKind::PrivateKeys), Some(vec![0x70, 0x02]));
    assert_eq!(path(DirectoryKind::PublicKeys), Some(vec![0x70, 0x04]));
    assert_eq!(path(DirectoryKind::Certificates), Some(vec![0x70, 0x05]));
}

#[test]
fn layout_matches_measured_token() {
    let layout = CardLayout::parse(&files()).unwrap();

    assert_eq!(layout.token_info.label.as_deref(), Some("VGCA Token"));
    assert_eq!(layout.token_info.serial_number, "23135568");

    let pin = layout.pins.iter().find(|pin| pin.label == "PIN").unwrap();
    assert_eq!(
        (pin.reference, pin.min_length, pin.max_length),
        (0x03, 4, Some(16))
    );
    assert_eq!(
        (pin.stored_length, pin.pad_char, pin.encoding),
        (16, Some(0xFF), PinEncoding::Utf8)
    );
    assert_eq!(
        layout
            .pins
            .iter()
            .find(|pin| pin.label == "PUK")
            .unwrap()
            .reference,
        0x06
    );

    let key = &layout.private_keys[0];
    assert_eq!(key.id, KEY_ID);
    assert_eq!(key.key_reference, Some(0x10));
    assert_eq!(key.modulus_bits, 3072);
    assert_eq!(key.auth_id.as_deref(), Some(pin.auth_id.as_slice()));

    assert_eq!(layout.public_keys[0].id, KEY_ID);
    assert_eq!(layout.public_keys[0].path, [0xDF, 0x10]);

    let certificate = &layout.certificates[0];
    assert_eq!(certificate.id, KEY_ID);
    assert_eq!(certificate.path, [0x00, 0x01]);
}
