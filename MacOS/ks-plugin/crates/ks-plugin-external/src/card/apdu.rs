use std::fmt;

use zeroize::Zeroize;

use super::transport::Transport;
use super::{CardError, CardResult};

pub const INS_SELECT: u8 = 0xA4;
pub const INS_READ_BINARY: u8 = 0xB0;
pub const INS_GET_RESPONSE: u8 = 0xC0;
pub const INS_VERIFY: u8 = 0x20;
pub const INS_MANAGE_SECURITY_ENVIRONMENT: u8 = 0x22;
pub const INS_PERFORM_SECURITY_OPERATION: u8 = 0x2A;

const ALLOWED_INSTRUCTIONS: [u8; 6] = [
    INS_SELECT,
    INS_READ_BINARY,
    INS_GET_RESPONSE,
    INS_VERIFY,
    INS_MANAGE_SECURITY_ENVIRONMENT,
    INS_PERFORM_SECURITY_OPERATION,
];

const SHORT_LE_MAX: usize = 256;
const SHORT_LC_MAX: usize = 255;
const READ_CHUNK: usize = 256;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct StatusWord(pub u16);

impl StatusWord {
    pub const SUCCESS: Self = Self(0x9000);
    pub const END_OF_FILE_REACHED: Self = Self(0x6282);
    pub const WRONG_OFFSET: Self = Self(0x6B00);
    pub const AUTHENTICATION_BLOCKED: Self = Self(0x6983);

    pub fn from_bytes(sw1: u8, sw2: u8) -> Self {
        Self(u16::from_be_bytes([sw1, sw2]))
    }

    pub fn sw1(self) -> u8 {
        self.0.to_be_bytes()[0]
    }

    pub fn sw2(self) -> u8 {
        self.0.to_be_bytes()[1]
    }

    pub fn is_success(self) -> bool {
        self == Self::SUCCESS
    }

    pub fn remaining_bytes(self) -> Option<usize> {
        (self.sw1() == 0x61).then(|| le_from_byte(self.sw2()))
    }

    pub fn pin_tries_left(self) -> Option<u8> {
        (self.sw1() == 0x63 && self.sw2() & 0xF0 == 0xC0).then(|| self.sw2() & 0x0F)
    }

    pub fn correct_length(self) -> Option<usize> {
        (self.sw1() == 0x6C).then(|| le_from_byte(self.sw2()))
    }
}

impl fmt::Display for StatusWord {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{:04X}", self.0)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Command<'a> {
    pub cla: u8,
    pub ins: u8,
    pub p1: u8,
    pub p2: u8,
    pub data: &'a [u8],
    pub le: Option<usize>,
}

impl<'a> Command<'a> {
    pub fn select_aid(aid: &'a [u8]) -> Self {
        Self::new(INS_SELECT, 0x04, 0x00, aid, Some(SHORT_LE_MAX))
    }

    pub fn select_file(fid: &'a [u8]) -> Self {
        Self::new(INS_SELECT, 0x00, 0x0C, fid, None)
    }

    pub fn select_path_from_mf(path: &'a [u8]) -> Self {
        Self::new(INS_SELECT, 0x08, 0x0C, path, None)
    }

    pub fn read_binary(offset: u16, le: usize) -> Self {
        let [p1, p2] = offset.to_be_bytes();
        Self::new(INS_READ_BINARY, p1, p2, &[], Some(le))
    }

    pub fn get_response(le: usize) -> Self {
        Self::new(INS_GET_RESPONSE, 0x00, 0x00, &[], Some(le))
    }

    pub fn verify(reference: u8, pin: &'a [u8]) -> Self {
        Self::new(INS_VERIFY, 0x00, reference, pin, None)
    }

    pub fn set_signature_environment(template: &'a [u8]) -> Self {
        Self::new(INS_MANAGE_SECURITY_ENVIRONMENT, 0x41, 0xB6, template, None)
    }

    pub fn compute_digital_signature(input: &'a [u8]) -> Self {
        Self::new(
            INS_PERFORM_SECURITY_OPERATION,
            0x9E,
            0x9A,
            input,
            Some(SHORT_LE_MAX),
        )
    }

    pub fn with_le(self, le: usize) -> Self {
        Self {
            le: Some(le),
            ..self
        }
    }

    fn new(ins: u8, p1: u8, p2: u8, data: &'a [u8], le: Option<usize>) -> Self {
        Self {
            cla: 0x00,
            ins,
            p1,
            p2,
            data,
            le,
        }
    }

    pub fn encode_into(&self, out: &mut Vec<u8>) -> CardResult<()> {
        let extended =
            self.data.len() > SHORT_LC_MAX || self.le.is_some_and(|le| le > SHORT_LE_MAX);
        if self.data.len() > usize::from(u16::MAX) || self.le.is_some_and(|le| le > 65_536) {
            return Err(CardError::Malformed("APDU vượt độ dài tối đa"));
        }

        out.clear();
        out.extend_from_slice(&[self.cla, self.ins, self.p1, self.p2]);

        if !self.data.is_empty() {
            if extended {
                out.push(0x00);
                out.extend_from_slice(&(self.data.len() as u16).to_be_bytes());
            } else {
                out.push(self.data.len() as u8);
            }
            out.extend_from_slice(self.data);
        }

        if let Some(le) = self.le {
            if extended {
                if self.data.is_empty() {
                    out.push(0x00);
                }
                out.extend_from_slice(&((le % 65_536) as u16).to_be_bytes());
            } else {
                out.push((le % SHORT_LE_MAX) as u8);
            }
        }

        Ok(())
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Response {
    pub data: Vec<u8>,
    pub status: StatusWord,
}

impl Response {
    pub fn parse(raw: &[u8]) -> CardResult<Self> {
        let [data @ .., sw1, sw2] = raw else {
            return Err(CardError::Malformed("phản hồi thiếu status word"));
        };
        Ok(Self {
            data: data.to_vec(),
            status: StatusWord::from_bytes(*sw1, *sw2),
        })
    }

    pub fn into_data(self) -> CardResult<Vec<u8>> {
        if self.status.is_success() {
            Ok(self.data)
        } else {
            Err(CardError::Status(self.status))
        }
    }
}

pub struct Channel<T> {
    transport: T,
    buffer: Vec<u8>,
}

impl<T: Transport> Channel<T> {
    pub fn new(transport: T) -> Self {
        Self {
            transport,
            buffer: Vec::with_capacity(SHORT_LE_MAX + 8),
        }
    }

    pub fn transport(&self) -> &T {
        &self.transport
    }

    pub fn send(&mut self, command: &Command<'_>) -> CardResult<Response> {
        if !ALLOWED_INSTRUCTIONS.contains(&command.ins) {
            return Err(CardError::InstructionNotAllowed(command.ins));
        }

        let mut response = self.transmit(command)?;

        if let Some(le) = response.status.correct_length() {
            response = self.transmit(&command.with_le(le))?;
        }

        while let Some(le) = response.status.remaining_bytes() {
            let next = self.transmit(&Command::get_response(le))?;
            response.data.extend_from_slice(&next.data);
            response.status = next.status;
        }

        Ok(response)
    }

    pub fn select_aid(&mut self, aid: &[u8]) -> CardResult<Vec<u8>> {
        self.send(&Command::select_aid(aid))?.into_data()
    }

    pub fn select_file(&mut self, fid: &[u8]) -> CardResult<()> {
        self.send(&Command::select_file(fid))?.into_data().map(drop)
    }

    pub fn select_path_from_mf(&mut self, path: &[u8]) -> CardResult<()> {
        self.send(&Command::select_path_from_mf(path))?
            .into_data()
            .map(drop)
    }

    pub fn read_selected_file(&mut self) -> CardResult<Vec<u8>> {
        self.read_selected_file_until(|_| None)
    }

    pub fn read_selected_file_until(
        &mut self,
        complete_length: impl Fn(&[u8]) -> Option<usize>,
    ) -> CardResult<Vec<u8>> {
        let mut content = Vec::new();
        loop {
            let offset =
                u16::try_from(content.len()).map_err(|_| CardError::Malformed("file quá lớn"))?;
            let response = self.send(&Command::read_binary(offset, READ_CHUNK))?;
            match response.status {
                StatusWord::SUCCESS => {
                    let chunk_len = response.data.len();
                    content.extend_from_slice(&response.data);
                    if let Some(length) = complete_length(&content) {
                        content.truncate(length);
                        return Ok(content);
                    }
                    if chunk_len < READ_CHUNK {
                        return Ok(content);
                    }
                }
                StatusWord::END_OF_FILE_REACHED => {
                    content.extend_from_slice(&response.data);
                    return Ok(content);
                }
                StatusWord::WRONG_OFFSET if !content.is_empty() => return Ok(content),
                status => return Err(CardError::Status(status)),
            }
        }
    }

    fn transmit(&mut self, command: &Command<'_>) -> CardResult<Response> {
        command.encode_into(&mut self.buffer)?;
        let reply = self.transport.transmit(&self.buffer);
        self.buffer.zeroize();
        Response::parse(&reply?)
    }
}

fn le_from_byte(byte: u8) -> usize {
    if byte == 0 {
        SHORT_LE_MAX
    } else {
        usize::from(byte)
    }
}

#[cfg(test)]
mod tests {
    use std::cell::RefCell;
    use std::collections::VecDeque;

    use super::*;

    struct ScriptedTransport {
        sent: RefCell<Vec<Vec<u8>>>,
        replies: RefCell<VecDeque<Vec<u8>>>,
    }

    impl ScriptedTransport {
        fn new(replies: &[&[u8]]) -> Self {
            Self {
                sent: RefCell::new(Vec::new()),
                replies: RefCell::new(replies.iter().map(|reply| reply.to_vec()).collect()),
            }
        }
    }

    impl Transport for ScriptedTransport {
        fn transmit(&self, apdu: &[u8]) -> CardResult<Vec<u8>> {
            self.sent.borrow_mut().push(apdu.to_vec());
            Ok(self.replies.borrow_mut().pop_front().unwrap())
        }
    }

    fn encode(command: Command<'_>) -> Vec<u8> {
        let mut out = Vec::new();
        command.encode_into(&mut out).unwrap();
        out
    }

    #[test]
    fn encodes_short_select_aid() {
        let aid = [0xE8, 0x28, 0xBD];

        assert_eq!(
            encode(Command::select_aid(&aid)),
            [0x00, 0xA4, 0x04, 0x00, 0x03, 0xE8, 0x28, 0xBD, 0x00]
        );
    }

    #[test]
    fn encodes_read_binary_offset() {
        assert_eq!(
            encode(Command::read_binary(0x0123, 256)),
            [0x00, 0xB0, 0x01, 0x23, 0x00]
        );
    }

    #[test]
    fn encodes_extended_le() {
        assert_eq!(
            encode(Command::read_binary(0, 384)),
            [0x00, 0xB0, 0x00, 0x00, 0x00, 0x01, 0x80]
        );
    }

    #[test]
    fn follows_get_response_chain() {
        let transport = ScriptedTransport::new(&[&[0x61, 0x02], &[0xAA, 0xBB, 0x90, 0x00]]);
        let mut channel = Channel::new(transport);

        let data = channel.select_aid(&[0xE8, 0x28]).unwrap();

        assert_eq!(data, [0xAA, 0xBB]);
        assert_eq!(
            channel.transport().sent.borrow()[1],
            [0x00, 0xC0, 0x00, 0x00, 0x02]
        );
    }

    #[test]
    fn retries_with_corrected_length() {
        let transport = ScriptedTransport::new(&[&[0x6C, 0x03], &[1, 2, 3, 0x90, 0x00]]);
        let mut channel = Channel::new(transport);

        let response = channel.send(&Command::read_binary(0, 256)).unwrap();

        assert_eq!(response.data, [1, 2, 3]);
        assert_eq!(
            channel.transport().sent.borrow()[1],
            [0x00, 0xB0, 0x00, 0x00, 0x03]
        );
    }

    #[test]
    fn reads_file_across_chunks() {
        let mut full_chunk = vec![0x11; 256];
        full_chunk.extend_from_slice(&[0x90, 0x00]);
        let transport = ScriptedTransport::new(&[&full_chunk, &[0x22, 0x33, 0x62, 0x82]]);
        let mut channel = Channel::new(transport);

        let content = channel.read_selected_file().unwrap();

        assert_eq!(content.len(), 258);
        assert_eq!(&content[256..], [0x22, 0x33]);
    }

    #[test]
    fn stops_reading_once_content_is_complete() {
        let mut full_chunk = vec![0x33; 256];
        full_chunk.extend_from_slice(&[0x90, 0x00]);
        let transport = ScriptedTransport::new(&[&full_chunk]);
        let mut channel = Channel::new(transport);

        let content = channel
            .read_selected_file_until(|content| Some(content.len().min(10)))
            .unwrap();

        assert_eq!(content.len(), 10);
        assert_eq!(channel.transport().sent.borrow().len(), 1);
    }

    #[test]
    fn reads_pin_tries_from_status() {
        assert_eq!(StatusWord(0x63C2).pin_tries_left(), Some(2));
        assert_eq!(StatusWord(0x6300).pin_tries_left(), None);
        assert_eq!(StatusWord::SUCCESS.pin_tries_left(), None);
    }

    #[test]
    fn rejects_instruction_outside_allowlist() {
        let mut channel = Channel::new(ScriptedTransport::new(&[]));
        let change_pin = Command {
            cla: 0x00,
            ins: 0x24,
            p1: 0x00,
            p2: 0x83,
            data: &[],
            le: None,
        };

        let error = channel.send(&change_pin).unwrap_err();

        assert!(matches!(error, CardError::InstructionNotAllowed(0x24)));
        assert!(channel.transport().sent.borrow().is_empty());
    }
}
