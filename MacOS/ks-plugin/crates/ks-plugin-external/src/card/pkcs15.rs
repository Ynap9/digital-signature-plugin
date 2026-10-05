use super::{CardError, CardResult};
use crate::tlv::{self, Tlv};

const TAG_SEQUENCE: u32 = 0x30;
const TAG_INTEGER: u32 = 0x02;
const TAG_BIT_STRING: u32 = 0x03;
const TAG_OCTET_STRING: u32 = 0x04;
const TAG_UTF8_STRING: u32 = 0x0C;
const TAG_ENUMERATED: u32 = 0x0A;
const TAG_CONTEXT_0: u32 = 0xA0;
const TAG_CONTEXT_1: u32 = 0xA1;
const TAG_TOKEN_LABEL: u32 = 0x80;
const TAG_PIN_REFERENCE: u32 = 0x80;
const TAG_APPLICATION_TEMPLATE: u32 = 0x61;
const TAG_APPLICATION_ID: u32 = 0x4F;
const TAG_APPLICATION_LABEL: u32 = 0x50;
const TAG_DISCRETIONARY_DATA: u32 = 0x73;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DirApplication {
    pub aid: Vec<u8>,
    pub label: String,
    pub odf_path: Option<Vec<u8>>,
    pub token_info_path: Option<Vec<u8>>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DirectoryKind {
    PrivateKeys,
    PublicKeys,
    TrustedPublicKeys,
    SecretKeys,
    Certificates,
    TrustedCertificates,
    UsefulCertificates,
    DataObjects,
    AuthObjects,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OdfEntry {
    pub kind: DirectoryKind,
    pub path: Vec<u8>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TokenInfo {
    pub version: u32,
    pub serial_number: String,
    pub manufacturer_id: Option<String>,
    pub label: Option<String>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum PinEncoding {
    Bcd,
    AsciiNumeric,
    Utf8,
    Other(u32),
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub struct PinFlags {
    pub local: bool,
    pub needs_padding: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PinInfo {
    pub label: String,
    pub flags: PinFlags,
    pub auth_id: Vec<u8>,
    pub encoding: PinEncoding,
    pub min_length: u32,
    pub stored_length: u32,
    pub max_length: Option<u32>,
    pub reference: u32,
    pub pad_char: Option<u8>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct KeyInfo {
    pub label: String,
    pub id: Vec<u8>,
    pub auth_id: Option<Vec<u8>>,
    pub key_reference: Option<u32>,
    pub path: Vec<u8>,
    pub modulus_bits: u32,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CertificateInfo {
    pub label: String,
    pub id: Vec<u8>,
    pub path: Vec<u8>,
}

struct CommonObject {
    label: String,
    auth_id: Option<Vec<u8>>,
}

pub fn parse_dir(bytes: &[u8]) -> CardResult<Vec<DirApplication>> {
    tlv::children(bytes)?
        .into_iter()
        .filter(|item| item.tag == TAG_APPLICATION_TEMPLATE)
        .map(|template| {
            let ddo = tlv::find(template.value, TAG_DISCRETIONARY_DATA)?;
            Ok(DirApplication {
                aid: tlv::require(template.value, TAG_APPLICATION_ID)?.to_vec(),
                label: tlv::find(template.value, TAG_APPLICATION_LABEL)?
                    .map(tlv::text)
                    .unwrap_or_default(),
                odf_path: ddo
                    .map(|ddo| path_in(ddo, TAG_SEQUENCE))
                    .transpose()?
                    .flatten(),
                token_info_path: ddo
                    .map(|ddo| path_in(ddo, TAG_CONTEXT_0))
                    .transpose()?
                    .flatten(),
            })
        })
        .collect()
}

pub fn parse_odf(bytes: &[u8]) -> CardResult<Vec<OdfEntry>> {
    tlv::children(bytes)?
        .into_iter()
        .filter_map(|item| directory_kind(item.tag).map(|kind| (kind, item)))
        .map(|(kind, item)| {
            let path = tlv::require(tlv::require(item.value, TAG_SEQUENCE)?, TAG_OCTET_STRING)?;
            Ok(OdfEntry {
                kind,
                path: path.to_vec(),
            })
        })
        .collect()
}

pub fn parse_token_info(bytes: &[u8]) -> CardResult<TokenInfo> {
    let fields = tlv::children(tlv::require(bytes, TAG_SEQUENCE)?)?;
    Ok(TokenInfo {
        version: tlv::unsigned(required_value(
            &fields,
            TAG_INTEGER,
            "TokenInfo thiếu version",
        )?)?,
        serial_number: tlv::text(required_value(
            &fields,
            TAG_OCTET_STRING,
            "TokenInfo thiếu serial",
        )?),
        manufacturer_id: value_of(&fields, TAG_UTF8_STRING).map(tlv::text),
        label: value_of(&fields, TAG_TOKEN_LABEL).map(tlv::text),
    })
}

pub fn parse_aodf(bytes: &[u8]) -> CardResult<Vec<PinInfo>> {
    objects(bytes)?
        .into_iter()
        .map(|object| {
            let [common, auth, type_attributes, ..] = object.as_slice() else {
                return Err(CardError::Malformed("đối tượng PIN thiếu thuộc tính"));
            };
            let pin = tlv::children(tlv::require(type_attributes.value, TAG_SEQUENCE)?)?;
            let integers: Vec<u32> = pin
                .iter()
                .filter(|item| item.tag == TAG_INTEGER)
                .map(|item| tlv::unsigned(item.value))
                .collect::<CardResult<_>>()?;
            let [min_length, stored_length, rest @ ..] = integers.as_slice() else {
                return Err(CardError::Malformed("PIN thiếu độ dài"));
            };
            Ok(PinInfo {
                label: common_object(common.value)?.label,
                flags: pin_flags(required_value(&pin, TAG_BIT_STRING, "PIN thiếu pinFlags")?),
                auth_id: tlv::require(auth.value, TAG_OCTET_STRING)?.to_vec(),
                encoding: pin_encoding(tlv::unsigned(required_value(
                    &pin,
                    TAG_ENUMERATED,
                    "PIN thiếu pinType",
                )?)?),
                min_length: *min_length,
                stored_length: *stored_length,
                max_length: rest.first().copied(),
                reference: tlv::unsigned(required_value(
                    &pin,
                    TAG_PIN_REFERENCE,
                    "PIN thiếu reference",
                )?)?,
                pad_char: value_of(&pin, TAG_OCTET_STRING).and_then(|pad| pad.first().copied()),
            })
        })
        .collect()
}

pub fn parse_key_directory(bytes: &[u8]) -> CardResult<Vec<KeyInfo>> {
    objects(bytes)?
        .into_iter()
        .map(|object| {
            let [common, key, .., type_attributes] = object.as_slice() else {
                return Err(CardError::Malformed("đối tượng khoá thiếu thuộc tính"));
            };
            let common = common_object(common.value)?;
            let key = tlv::children(key.value)?;
            let reference = key.iter().rev().find(|item| item.tag == TAG_INTEGER);
            let (path, modulus_bits) = key_location(type_attributes)?;
            Ok(KeyInfo {
                label: common.label,
                id: required_value(&key, TAG_OCTET_STRING, "khoá thiếu ID")?.to_vec(),
                auth_id: common.auth_id,
                key_reference: reference
                    .map(|item| tlv::unsigned(item.value))
                    .transpose()?,
                path,
                modulus_bits,
            })
        })
        .collect()
}

pub fn parse_cdf(bytes: &[u8]) -> CardResult<Vec<CertificateInfo>> {
    objects(bytes)?
        .into_iter()
        .map(|object| {
            let [common, certificate, .., type_attributes] = object.as_slice() else {
                return Err(CardError::Malformed("đối tượng chứng thư thiếu thuộc tính"));
            };
            let location = tlv::require(type_attributes.value, TAG_SEQUENCE)?;
            Ok(CertificateInfo {
                label: common_object(common.value)?.label,
                id: tlv::require(certificate.value, TAG_OCTET_STRING)?.to_vec(),
                path: required_path(location)?,
            })
        })
        .collect()
}

fn objects(bytes: &[u8]) -> CardResult<Vec<Vec<Tlv<'_>>>> {
    tlv::children(bytes)?
        .into_iter()
        .map(|object| tlv::children(object.value))
        .collect()
}

fn common_object(bytes: &[u8]) -> CardResult<CommonObject> {
    Ok(CommonObject {
        label: tlv::find(bytes, TAG_UTF8_STRING)?
            .map(tlv::text)
            .unwrap_or_default(),
        auth_id: tlv::find(bytes, TAG_OCTET_STRING)?.map(<[u8]>::to_vec),
    })
}

fn key_location(type_attributes: &Tlv<'_>) -> CardResult<(Vec<u8>, u32)> {
    if type_attributes.tag != TAG_CONTEXT_1 {
        return Err(CardError::Malformed("khoá thiếu typeAttributes"));
    }
    let location = tlv::require(type_attributes.value, TAG_SEQUENCE)?;
    let modulus = tlv::require(location, TAG_INTEGER)?;
    Ok((required_path(location)?, tlv::unsigned(modulus)?))
}

fn required_path(bytes: &[u8]) -> CardResult<Vec<u8>> {
    path_in(bytes, TAG_SEQUENCE)?.ok_or(CardError::Malformed("thiếu đường dẫn file"))
}

fn path_in(bytes: &[u8], tag: u32) -> CardResult<Option<Vec<u8>>> {
    tlv::find(bytes, tag)?
        .map(|path| tlv::require(path, TAG_OCTET_STRING).map(<[u8]>::to_vec))
        .transpose()
}

fn required_value<'a>(items: &[Tlv<'a>], tag: u32, missing: &'static str) -> CardResult<&'a [u8]> {
    value_of(items, tag).ok_or(CardError::Malformed(missing))
}

fn value_of<'a>(items: &[Tlv<'a>], tag: u32) -> Option<&'a [u8]> {
    items
        .iter()
        .find(|item| item.tag == tag)
        .map(|item| item.value)
}

fn directory_kind(tag: u32) -> Option<DirectoryKind> {
    Some(match tag {
        0xA0 => DirectoryKind::PrivateKeys,
        0xA1 => DirectoryKind::PublicKeys,
        0xA2 => DirectoryKind::TrustedPublicKeys,
        0xA3 => DirectoryKind::SecretKeys,
        0xA4 => DirectoryKind::Certificates,
        0xA5 => DirectoryKind::TrustedCertificates,
        0xA6 => DirectoryKind::UsefulCertificates,
        0xA7 => DirectoryKind::DataObjects,
        0xA8 => DirectoryKind::AuthObjects,
        _ => return None,
    })
}

fn pin_flags(bit_string: &[u8]) -> PinFlags {
    let bit = |index: usize| {
        bit_string
            .get(1 + index / 8)
            .is_some_and(|byte| byte & (0x80 >> (index % 8)) != 0)
    };
    PinFlags {
        local: bit(1),
        needs_padding: bit(5),
    }
}

fn pin_encoding(value: u32) -> PinEncoding {
    match value {
        0 => PinEncoding::Bcd,
        1 => PinEncoding::AsciiNumeric,
        2 => PinEncoding::Utf8,
        other => PinEncoding::Other(other),
    }
}
