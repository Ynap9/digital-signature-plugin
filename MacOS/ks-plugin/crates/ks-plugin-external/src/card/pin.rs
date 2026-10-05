use std::fmt;

use zeroize::Zeroizing;

use super::pkcs15::PinInfo;
use super::{CardError, CardResult};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct PinPadding {
    pub length: usize,
    pub byte: u8,
}

pub struct Pin(Zeroizing<Vec<u8>>);

impl Pin {
    pub fn new(text: &str, info: &PinInfo) -> CardResult<Self> {
        let bytes = Zeroizing::new(text.as_bytes().to_vec());
        let length = u32::try_from(bytes.len()).map_err(|_| CardError::InvalidPinFormat)?;
        let max_length = info.max_length.unwrap_or(info.stored_length);
        if length < info.min_length || length > max_length || bytes.contains(&0) {
            return Err(CardError::InvalidPinFormat);
        }
        Ok(Self(bytes))
    }

    pub fn encode(&self, padding: Option<PinPadding>) -> Zeroizing<Vec<u8>> {
        let mut encoded = Zeroizing::new(self.0.to_vec());
        if let Some(padding) = padding {
            let length = padding.length.max(encoded.len());
            encoded.resize(length, padding.byte);
        }
        encoded
    }
}

impl fmt::Debug for Pin {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str("Pin(***)")
    }
}
