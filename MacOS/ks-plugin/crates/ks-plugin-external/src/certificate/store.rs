use std::io::ErrorKind;
use std::path::PathBuf;

use super::CertificateError;

const EXTENSION: &str = "cer";

pub struct CertificateStore {
    dir: PathBuf,
}

impl CertificateStore {
    pub fn new(dir: impl Into<PathBuf>) -> Self {
        Self { dir: dir.into() }
    }

    pub fn load(&self, key_id: &[u8]) -> Result<Option<Vec<u8>>, CertificateError> {
        match std::fs::read(self.path_for(key_id)) {
            Ok(der) => Ok(Some(der)),
            Err(error) if error.kind() == ErrorKind::NotFound => Ok(None),
            Err(error) => Err(error.into()),
        }
    }

    pub fn save(&self, key_id: &[u8], der: &[u8]) -> Result<(), CertificateError> {
        std::fs::create_dir_all(&self.dir)?;
        std::fs::write(self.path_for(key_id), der)?;
        Ok(())
    }

    fn path_for(&self, key_id: &[u8]) -> PathBuf {
        self.dir
            .join(format!("{}.{EXTENSION}", hex::encode_upper(key_id)))
    }
}
