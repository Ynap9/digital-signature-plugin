use std::cell::RefCell;
use std::ffi::{CStr, CString};
use std::time::Duration;

use pcsc::{
    Attribute, Card, Context, Protocols, ReaderState, Scope, ShareMode, State, Transaction,
};

use super::{CardError, CardResult};

pub trait Transport {
    fn transmit(&self, apdu: &[u8]) -> CardResult<Vec<u8>>;
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ReaderInfo {
    pub name: String,
    pub atr: Option<Vec<u8>>,
}

pub struct PcscReaders {
    context: Context,
}

impl PcscReaders {
    pub fn establish() -> CardResult<Self> {
        Ok(Self {
            context: Context::establish(Scope::User)?,
        })
    }

    pub fn list(&self) -> CardResult<Vec<ReaderInfo>> {
        let names = match self.context.list_readers_owned() {
            Ok(names) => names,
            Err(pcsc::Error::NoReadersAvailable) => return Ok(Vec::new()),
            Err(error) => return Err(error.into()),
        };

        let mut states: Vec<ReaderState> = names
            .iter()
            .map(|name| ReaderState::new(name.as_c_str(), State::UNAWARE))
            .collect();
        self.context
            .get_status_change(Duration::ZERO, &mut states)?;

        Ok(states
            .iter()
            .map(|state| ReaderInfo {
                name: state.name().to_string_lossy().into_owned(),
                atr: state
                    .event_state()
                    .contains(State::PRESENT)
                    .then(|| state.atr().to_vec()),
            })
            .collect())
    }

    pub fn connect(&self, reader: &str) -> CardResult<PcscTransport> {
        let name =
            CString::new(reader).map_err(|_| CardError::Malformed("tên đầu đọc chứa ký tự NUL"))?;
        PcscTransport::connect(&self.context, &name, reader)
    }
}

pub struct PcscTransport {
    card: Card,
    buffer: RefCell<Vec<u8>>,
}

impl PcscTransport {
    fn connect(context: &Context, name: &CStr, reader: &str) -> CardResult<Self> {
        let card = match context.connect(name, ShareMode::Shared, Protocols::ANY) {
            Ok(card) => card,
            Err(pcsc::Error::NoSmartcard | pcsc::Error::RemovedCard) => {
                return Err(CardError::NoCard(reader.to_owned()));
            }
            Err(error) => return Err(error.into()),
        };
        Ok(Self {
            card,
            buffer: RefCell::new(vec![0; pcsc::MAX_BUFFER_SIZE_EXTENDED]),
        })
    }

    pub fn atr(&self) -> CardResult<Vec<u8>> {
        Ok(self.card.get_attribute_owned(Attribute::AtrString)?)
    }

    pub fn transaction(&mut self) -> CardResult<PcscTransaction<'_>> {
        Ok(PcscTransaction {
            transaction: self.card.transaction()?,
            buffer: &self.buffer,
        })
    }
}

impl Transport for PcscTransport {
    fn transmit(&self, apdu: &[u8]) -> CardResult<Vec<u8>> {
        transmit(&self.card, &self.buffer, apdu)
    }
}

pub struct PcscTransaction<'a> {
    transaction: Transaction<'a>,
    buffer: &'a RefCell<Vec<u8>>,
}

impl Transport for PcscTransaction<'_> {
    fn transmit(&self, apdu: &[u8]) -> CardResult<Vec<u8>> {
        transmit(&self.transaction, self.buffer, apdu)
    }
}

fn transmit(card: &Card, buffer: &RefCell<Vec<u8>>, apdu: &[u8]) -> CardResult<Vec<u8>> {
    let mut buffer = buffer.borrow_mut();
    Ok(card.transmit(apdu, &mut buffer)?.to_vec())
}
