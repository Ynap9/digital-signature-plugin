use std::time::SystemTime;

use base64::Engine;
use base64::engine::general_purpose::STANDARD;
use sha1::{Digest, Sha1};
use x509_cert::Certificate;
use x509_cert::der::asn1::ObjectIdentifier;
use x509_cert::der::{Any, Decode, Tag, Tagged};
use x509_cert::ext::pkix::KeyUsage;
use x509_cert::name::Name;

use super::{CertificateError, RsaPublicKey};
use crate::tlv;

const OID_RSA_ENCRYPTION: ObjectIdentifier = ObjectIdentifier::new_unwrap("1.2.840.113549.1.1.1");
const OID_KEY_USAGE: ObjectIdentifier = ObjectIdentifier::new_unwrap("2.5.29.15");
const OID_COMMON_NAME: ObjectIdentifier = ObjectIdentifier::new_unwrap("2.5.4.3");

const ATTRIBUTE_NAMES: [(&str, &str); 12] = [
    ("2.5.4.3", "CN"),
    ("2.5.4.4", "SN"),
    ("2.5.4.5", "SERIALNUMBER"),
    ("2.5.4.6", "C"),
    ("2.5.4.7", "L"),
    ("2.5.4.8", "S"),
    ("2.5.4.9", "STREET"),
    ("2.5.4.10", "O"),
    ("2.5.4.11", "OU"),
    ("2.5.4.12", "T"),
    ("2.5.4.42", "G"),
    ("1.2.840.113549.1.9.1", "E"),
];

const PEM_BEGIN: &str = "-----BEGIN CERTIFICATE-----";
const PEM_END: &str = "-----END CERTIFICATE-----";

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CertificateDetails {
    pub der: Vec<u8>,
    pub thumbprint: String,
    pub subject: String,
    pub common_name: String,
    pub issuer: String,
    pub issuer_common_name: String,
    pub serial_number: String,
    pub not_before: SystemTime,
    pub not_after: SystemTime,
    pub allows_signing: bool,
    pub public_key: RsaPublicKey,
}

impl CertificateDetails {
    pub fn from_file_bytes(bytes: &[u8]) -> Result<Self, CertificateError> {
        match std::str::from_utf8(bytes) {
            Ok(text) if text.trim_start().starts_with(PEM_BEGIN) => {
                Self::from_der(pem_to_der(text)?)
            }
            _ => Self::from_der(bytes.to_vec()),
        }
    }

    pub fn from_der(der: Vec<u8>) -> Result<Self, CertificateError> {
        let certificate =
            Certificate::from_der(&der).map_err(|_| CertificateError::InvalidEncoding)?;
        let tbs = &certificate.tbs_certificate;
        let spki = &tbs.subject_public_key_info;
        if spki.algorithm.oid != OID_RSA_ENCRYPTION {
            return Err(CertificateError::NotRsa);
        }

        Ok(Self {
            thumbprint: hex::encode_upper(Sha1::digest(&der)),
            subject: distinguished_name(&tbs.subject),
            common_name: common_name(&tbs.subject),
            issuer: distinguished_name(&tbs.issuer),
            issuer_common_name: common_name(&tbs.issuer),
            serial_number: hex::encode_upper(tbs.serial_number.as_bytes()),
            not_before: tbs.validity.not_before.to_system_time(),
            not_after: tbs.validity.not_after.to_system_time(),
            allows_signing: allows_signing(&certificate)?,
            public_key: pkcs1_public_key(spki.subject_public_key.raw_bytes())?,
            der,
        })
    }

    pub fn is_valid_at(&self, now: SystemTime) -> bool {
        self.not_before <= now && now <= self.not_after
    }
}

fn pem_to_der(text: &str) -> Result<Vec<u8>, CertificateError> {
    let body = text
        .trim()
        .strip_prefix(PEM_BEGIN)
        .and_then(|rest| rest.split(PEM_END).next())
        .ok_or(CertificateError::InvalidEncoding)?;
    let base64: String = body.chars().filter(|c| !c.is_whitespace()).collect();
    STANDARD
        .decode(base64)
        .map_err(|_| CertificateError::InvalidEncoding)
}

fn allows_signing(certificate: &Certificate) -> Result<bool, CertificateError> {
    let extensions = certificate
        .tbs_certificate
        .extensions
        .as_deref()
        .unwrap_or_default();
    let Some(extension) = extensions
        .iter()
        .find(|extension| extension.extn_id == OID_KEY_USAGE)
    else {
        return Ok(true);
    };
    let usage = KeyUsage::from_der(extension.extn_value.as_bytes())
        .map_err(|_| CertificateError::InvalidEncoding)?;
    Ok(usage.digital_signature() || usage.non_repudiation())
}

fn pkcs1_public_key(bytes: &[u8]) -> Result<RsaPublicKey, CertificateError> {
    let malformed = |_| CertificateError::InvalidEncoding;
    let sequence = tlv::require(bytes, 0x30).map_err(malformed)?;
    let integers = tlv::children(sequence).map_err(malformed)?;
    match integers.as_slice() {
        [modulus, exponent] if modulus.tag == 0x02 && exponent.tag == 0x02 => {
            Ok(RsaPublicKey::new(modulus.value, exponent.value))
        }
        _ => Err(CertificateError::InvalidEncoding),
    }
}

fn distinguished_name(name: &Name) -> String {
    name.0
        .iter()
        .rev()
        .flat_map(|rdn| rdn.0.iter())
        .map(|attribute| {
            let oid = attribute.oid.to_string();
            let label = ATTRIBUTE_NAMES
                .iter()
                .find(|(known, _)| *known == oid)
                .map_or(oid.as_str(), |(_, short)| short);
            format!(
                "{label}={}",
                quote_if_needed(&attribute_text(&attribute.value))
            )
        })
        .collect::<Vec<_>>()
        .join(", ")
}

fn common_name(name: &Name) -> String {
    name.0
        .iter()
        .rev()
        .flat_map(|rdn| rdn.0.iter())
        .find(|attribute| attribute.oid == OID_COMMON_NAME)
        .map(|attribute| attribute_text(&attribute.value))
        .unwrap_or_default()
}

fn attribute_text(value: &Any) -> String {
    let (tag, value) = (value.tag(), value.value());
    if tag == Tag::BmpString {
        let units: Vec<u16> = value
            .as_chunks::<2>()
            .0
            .iter()
            .map(|pair| u16::from_be_bytes(*pair))
            .collect();
        return String::from_utf16_lossy(&units);
    }
    String::from_utf8_lossy(value).into_owned()
}

fn quote_if_needed(value: &str) -> String {
    let needs_quotes = value.is_empty()
        || value.starts_with(' ')
        || value.ends_with(' ')
        || value.contains([',', '+', '=', '"', '\n', '<', '>', '#', ';']);
    if needs_quotes {
        format!("\"{}\"", value.replace('"', "\"\""))
    } else {
        value.to_owned()
    }
}
