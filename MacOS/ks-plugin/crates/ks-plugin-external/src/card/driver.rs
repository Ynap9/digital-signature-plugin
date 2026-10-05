use sha2::{Digest, Sha256};

use super::apdu::{Channel, Command, StatusWord};
use super::pin::{Pin, PinPadding};
use super::pkcs15::{
    self, CertificateInfo, DirApplication, DirectoryKind, KeyInfo, OdfEntry, PinInfo, TokenInfo,
};
use super::transport::Transport;
use super::{CardError, CardResult};
use crate::certificate::RsaPublicKey;
use crate::tlv;

pub const VGCA_APPLET_AID: [u8; 10] = [0xE8, 0x28, 0xBD, 0x08, 0x0F, 0x01, 0x4E, 0x58, 0x50, 0x30];

const EF_DIR_PATH: [u8; 2] = [0x2F, 0x00];
const PUBLIC_EXPONENT_LENGTH: usize = 4;
const LOCAL_REFERENCE_FLAG: u8 = 0x80;
const TAG_PRIVATE_KEY_REFERENCE: u8 = 0x84;
const TAG_ALGORITHM_REFERENCE: u8 = 0x80;
const SHA256_DIGEST_INFO_PREFIX: [u8; 19] = [
    0x30, 0x31, 0x30, 0x0D, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05,
    0x00, 0x04, 0x20,
];

pub const MIN_PIN_TRIES_TO_VERIFY: u8 = 2;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum PinStatus {
    Verified,
    TriesLeft(u8),
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct SignScheme {
    pub algorithm: Option<u8>,
    pub send_digest_info: bool,
}
const ATR_MARKER: &[u8] = b"B4D";

pub fn matches_atr(atr: &[u8]) -> bool {
    atr.windows(ATR_MARKER.len())
        .any(|window| window == ATR_MARKER)
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CardFiles {
    pub dir: Vec<u8>,
    pub odf: Vec<u8>,
    pub token_info: Vec<u8>,
    pub aodf: Vec<u8>,
    pub prkdf: Vec<u8>,
    pub pukdf: Vec<u8>,
    pub cdf: Vec<u8>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CardLayout {
    pub application: DirApplication,
    pub token_info: TokenInfo,
    pub pins: Vec<PinInfo>,
    pub private_keys: Vec<KeyInfo>,
    pub public_keys: Vec<KeyInfo>,
    pub certificates: Vec<CertificateInfo>,
}

impl CardLayout {
    pub fn parse(files: &CardFiles) -> CardResult<Self> {
        let application = vgca_application(&files.dir)?;
        Ok(Self {
            application,
            token_info: pkcs15::parse_token_info(&files.token_info)?,
            pins: pkcs15::parse_aodf(&files.aodf)?,
            private_keys: pkcs15::parse_key_directory(&files.prkdf)?,
            public_keys: pkcs15::parse_key_directory(&files.pukdf)?,
            certificates: pkcs15::parse_cdf(&files.cdf)?,
        })
    }
}

pub struct Bit4idVgcaDriver<T> {
    channel: Channel<T>,
}

impl<T: Transport> Bit4idVgcaDriver<T> {
    pub fn new(transport: T) -> Self {
        Self {
            channel: Channel::new(transport),
        }
    }

    pub fn read_files(&mut self) -> CardResult<CardFiles> {
        self.channel.select_aid(&VGCA_APPLET_AID)?;
        self.channel.select_path_from_mf(&EF_DIR_PATH)?;
        let dir = self.channel.read_selected_file_until(tlv::padded_length)?;
        let application = vgca_application(&dir)?;
        let odf_path = application
            .odf_path
            .ok_or(CardError::Malformed("EF.DIR thiếu đường dẫn ODF"))?;
        let token_info_path = application
            .token_info_path
            .ok_or(CardError::Malformed("EF.DIR thiếu đường dẫn TokenInfo"))?;

        self.channel.select_aid(&VGCA_APPLET_AID)?;
        let odf = self.read_directory(&odf_path)?;
        let entries = pkcs15::parse_odf(&odf)?;
        let directory = |kind| directory_path(&entries, kind);

        Ok(CardFiles {
            token_info: self.read_directory(&token_info_path)?,
            aodf: self.read_directory(directory(DirectoryKind::AuthObjects)?)?,
            prkdf: self.read_directory(directory(DirectoryKind::PrivateKeys)?)?,
            pukdf: self.read_directory(directory(DirectoryKind::PublicKeys)?)?,
            cdf: self.read_directory(directory(DirectoryKind::Certificates)?)?,
            dir,
            odf,
        })
    }

    pub fn read_file(&mut self, path: &[u8]) -> CardResult<Vec<u8>> {
        self.channel.select_file(path)?;
        self.channel.read_selected_file()
    }

    pub fn read_certificate_file(&mut self, path: &[u8]) -> CardResult<Vec<u8>> {
        self.channel.select_file(path)?;
        self.channel
            .read_selected_file_until(tlv::single_item_length)
    }

    pub fn read_public_key(&mut self, key: &KeyInfo) -> CardResult<RsaPublicKey> {
        let content = self.read_file(&key.path)?;
        let modulus_length = usize::try_from(key.modulus_bits / 8)
            .map_err(|_| CardError::Malformed("độ dài khoá"))?;
        if content.len() != PUBLIC_EXPONENT_LENGTH + modulus_length {
            return Err(CardError::Malformed("file khoá công khai sai độ dài"));
        }
        let (exponent, modulus) = content.split_at(PUBLIC_EXPONENT_LENGTH);
        Ok(RsaPublicKey::new(modulus, exponent))
    }

    pub fn pin_status(&mut self, info: &PinInfo) -> CardResult<PinStatus> {
        let status = self
            .channel
            .send(&Command::verify(pin_reference(info)?, &[]))?
            .status;
        match status {
            StatusWord::SUCCESS => Ok(PinStatus::Verified),
            StatusWord::AUTHENTICATION_BLOCKED => Err(CardError::PinBlocked),
            status => status
                .pin_tries_left()
                .map(PinStatus::TriesLeft)
                .ok_or(CardError::PinStatusUnknown(status)),
        }
    }

    pub fn verify_pin(&mut self, info: &PinInfo, pin: &Pin) -> CardResult<()> {
        match self.pin_status(info)? {
            PinStatus::Verified => return Ok(()),
            PinStatus::TriesLeft(tries) if tries < MIN_PIN_TRIES_TO_VERIFY => {
                return Err(CardError::TooFewPinTries(tries));
            }
            PinStatus::TriesLeft(_) => {}
        }

        let encoded = pin.encode(pin_padding(info));
        let status = self
            .channel
            .send(&Command::verify(pin_reference(info)?, &encoded))?
            .status;
        match status {
            StatusWord::SUCCESS => Ok(()),
            StatusWord::AUTHENTICATION_BLOCKED => Err(CardError::PinBlocked),
            status => Err(status
                .pin_tries_left()
                .map_or(CardError::Status(status), CardError::WrongPin)),
        }
    }

    pub fn sign_sha256(
        &mut self,
        key: &KeyInfo,
        data: &[u8],
        scheme: SignScheme,
    ) -> CardResult<Vec<u8>> {
        let key_reference = key
            .key_reference
            .and_then(|reference| u8::try_from(reference).ok())
            .ok_or(CardError::Malformed("khoá thiếu key reference"))?;
        let mut template = vec![TAG_PRIVATE_KEY_REFERENCE, 0x01, key_reference];
        if let Some(algorithm) = scheme.algorithm {
            template.extend_from_slice(&[TAG_ALGORITHM_REFERENCE, 0x01, algorithm]);
        }
        self.channel
            .send(&Command::set_signature_environment(&template))?
            .into_data()?;

        let digest = Sha256::digest(data);
        let input = if scheme.send_digest_info {
            [SHA256_DIGEST_INFO_PREFIX.as_slice(), digest.as_slice()].concat()
        } else {
            digest.to_vec()
        };
        self.channel
            .send(&Command::compute_digital_signature(&input))?
            .into_data()
    }

    fn read_directory(&mut self, path: &[u8]) -> CardResult<Vec<u8>> {
        self.channel.select_file(path)?;
        self.channel.read_selected_file_until(tlv::padded_length)
    }

    pub fn transport(&self) -> &T {
        self.channel.transport()
    }
}

// bit4id applet compares all storedLength bytes even though AODF leaves needs-padding unset.
fn pin_padding(info: &PinInfo) -> Option<PinPadding> {
    Some(PinPadding {
        length: usize::try_from(info.stored_length).ok()?,
        byte: info.pad_char?,
    })
}

fn pin_reference(info: &PinInfo) -> CardResult<u8> {
    let reference =
        u8::try_from(info.reference).map_err(|_| CardError::Malformed("PIN reference quá lớn"))?;
    Ok(if info.flags.local {
        reference | LOCAL_REFERENCE_FLAG
    } else {
        reference
    })
}

fn vgca_application(dir: &[u8]) -> CardResult<DirApplication> {
    pkcs15::parse_dir(dir)?
        .into_iter()
        .find(|application| application.aid == VGCA_APPLET_AID)
        .ok_or(CardError::Malformed("EF.DIR không có applet VGCA"))
}

fn directory_path(entries: &[OdfEntry], kind: DirectoryKind) -> CardResult<&[u8]> {
    entries
        .iter()
        .find(|entry| entry.kind == kind)
        .map(|entry| entry.path.as_slice())
        .ok_or(CardError::Malformed("ODF thiếu thư mục bắt buộc"))
}
