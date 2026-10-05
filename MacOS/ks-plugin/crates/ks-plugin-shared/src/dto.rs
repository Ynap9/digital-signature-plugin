use serde::Serialize;
use serde_repr::Serialize_repr;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize_repr)]
#[repr(u8)]
pub enum CertSource {
    Local = 0,
    Server = 1,
    UsbToken = 2,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SignCertDto {
    pub subject: String,
    pub common_name: String,
    pub issuer: String,
    pub issuer_common_name: String,
    pub serial_number: String,
    pub thumbprint: String,
    pub source: CertSource,
    pub key_provider: Option<String>,
    pub valid_from: String,
    pub valid_to: String,
    pub has_private_key: bool,
    pub is_expired: bool,
    pub allows_signing: bool,
    pub reason: Option<String>,
}
