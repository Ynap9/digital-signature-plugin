use thiserror::Error;

use super::apdu::StatusWord;

pub type CardResult<T> = Result<T, CardError>;

#[derive(Debug, Error)]
pub enum CardError {
    #[error("Lỗi PC/SC: {0}")]
    Pcsc(#[from] pcsc::Error),
    #[error("Không thấy đầu đọc thẻ nào")]
    NoReader,
    #[error("Chưa cắm token vào đầu đọc {0}")]
    NoCard(String),
    #[error("Thẻ trả mã lỗi {0}")]
    Status(StatusWord),
    #[error("Lệnh APDU {0:#04X} không nằm trong danh sách được phép")]
    InstructionNotAllowed(u8),
    #[error("Dữ liệu thẻ sai định dạng: {0}")]
    Malformed(&'static str),
    #[error("PIN phải dài 4–16 ký tự và không chứa ký tự rỗng")]
    InvalidPinFormat,
    #[error("Sai PIN, token còn {0} lần thử")]
    WrongPin(u8),
    #[error(
        "Token chỉ còn {0} lần thử PIN — nhập đúng PIN bằng công cụ của hãng để khôi phục trước khi dùng plugin"
    )]
    TooFewPinTries(u8),
    #[error("PIN đã bị khoá — cần mở khoá bằng PUK")]
    PinBlocked,
    #[error("Không đọc được số lần thử PIN (thẻ trả {0})")]
    PinStatusUnknown(StatusWord),
    #[error("Chữ ký thẻ trả về không khớp khoá công khai")]
    InvalidSignature,
}
