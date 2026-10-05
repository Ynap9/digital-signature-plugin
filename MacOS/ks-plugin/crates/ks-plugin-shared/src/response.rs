use serde::{Deserialize, Serialize};
use serde_repr::{Deserialize_repr, Serialize_repr};

pub const SUCCESS_CODE: u16 = 200;
pub const ERROR_CODE: u16 = 500;
pub const SUCCESS_MESSAGE: &str = "Ok";

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize_repr, Deserialize_repr)]
#[repr(u8)]
pub enum ResponseStatus {
    Error = 0,
    Success = 1,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ApiResponse<T> {
    pub status: ResponseStatus,
    pub data: Option<T>,
    pub code: u16,
    pub message: String,
}

impl<T> ApiResponse<T> {
    pub fn ok(data: T) -> Self {
        Self {
            status: ResponseStatus::Success,
            data: Some(data),
            code: SUCCESS_CODE,
            message: SUCCESS_MESSAGE.to_owned(),
        }
    }

    pub fn error(message: impl Into<String>) -> Self {
        Self {
            status: ResponseStatus::Error,
            data: None,
            code: ERROR_CODE,
            message: message.into(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::{Value, json};

    #[test]
    fn ok_serializes_to_backend_envelope() {
        let response = ApiResponse::ok(json!({}));

        let text = serde_json::to_string(&response).unwrap();

        assert_eq!(text, r#"{"status":1,"data":{},"code":200,"message":"Ok"}"#);
    }

    #[test]
    fn error_serializes_with_null_data() {
        let response = ApiResponse::<Value>::error("Không tìm thấy chứng thư số");

        let value = serde_json::to_value(&response).unwrap();

        assert_eq!(
            value,
            json!({ "status": 0, "data": null, "code": 500, "message": "Không tìm thấy chứng thư số" })
        );
    }

    #[test]
    fn status_round_trips_as_number() {
        let response: ApiResponse<bool> =
            serde_json::from_str(r#"{"status":1,"data":true,"code":200,"message":"Ok"}"#).unwrap();

        assert_eq!(response, ApiResponse::ok(true));
    }
}
