pub mod sign_cert;
pub mod store;
pub mod x509;

use rsa::{BigUint, Pkcs1v15Sign};
use sha2::{Digest, Sha256};
use thiserror::Error;

pub use x509::CertificateDetails;

#[derive(Debug, Error)]
pub enum CertificateError {
    #[error("File không phải chứng thư số X.509 hợp lệ")]
    InvalidEncoding,
    #[error("Chứng thư số không dùng khoá RSA")]
    NotRsa,
    #[error("Chứng thư số không khớp khoá trên token đang cắm")]
    KeyMismatch,
    #[error("Không đọc/ghi được chứng thư đã nạp: {0}")]
    Storage(#[from] std::io::Error),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RsaPublicKey {
    pub modulus: Vec<u8>,
    pub exponent: Vec<u8>,
}

impl RsaPublicKey {
    pub fn new(modulus: &[u8], exponent: &[u8]) -> Self {
        Self {
            modulus: trim_leading_zeros(modulus).to_vec(),
            exponent: trim_leading_zeros(exponent).to_vec(),
        }
    }

    pub fn verify_pkcs1_sha256(&self, message: &[u8], signature: &[u8]) -> bool {
        let modulus = BigUint::from_bytes_be(&self.modulus);
        let exponent = BigUint::from_bytes_be(&self.exponent);
        rsa::RsaPublicKey::new(modulus, exponent).is_ok_and(|key| {
            key.verify(
                Pkcs1v15Sign::new::<Sha256>(),
                &Sha256::digest(message),
                signature,
            )
            .is_ok()
        })
    }

    pub fn modulus_bits(&self) -> usize {
        self.modulus.first().map_or(0, |first| {
            self.modulus.len() * 8 - first.leading_zeros() as usize
        })
    }
}

pub fn verify_matches_card(
    certificate: CertificateDetails,
    card_key: &RsaPublicKey,
) -> Result<CertificateDetails, CertificateError> {
    if &certificate.public_key == card_key {
        Ok(certificate)
    } else {
        Err(CertificateError::KeyMismatch)
    }
}

fn trim_leading_zeros(bytes: &[u8]) -> &[u8] {
    let start = bytes
        .iter()
        .position(|byte| *byte != 0)
        .unwrap_or(bytes.len());
    &bytes[start..]
}
