# Coding conventions

> **Phần 6/7** · trước: [05-macos-platform.md](05-macos-platform.md) · mục lục: [README.md](README.md)

## Naming

| Thứ | Quy ước | Ví dụ |
|---|---|---|
| Crate, module, trait, struct, enum, function, const, thread, tool, thuật ngữ kỹ thuật (session, worker, channel, handler, layer…) | **Tiếng Anh**, theo chuẩn Rust (`snake_case`, `CamelCase`, `SCREAMING_CASE`) | `CardDriver`, `SigningSession`, `card-worker`, `SESSION_IDLE_TIMEOUT_MINUTES` |
| Route, tên trường JSON | **Giữ nguyên tiếng Việt** của hợp đồng | `api/plugin/ky-so/mo-phien`, `chungThuBase64` |
| Câu báo lỗi cho người dùng | Tiếng Việt, đọc được, không lộ chi tiết nội bộ | "Token chỉ còn 1 lần thử PIN" |
| Tài liệu | Tiếng Việt | — |

Tên trường Rust tiếng Anh, tên JSON tiếng Việt ⇒ nối bằng `#[serde(rename = "…")]` từng trường, ví dụ
`SignRequestDto { #[serde(rename = "yeuCauId")] request_id, #[serde(rename = "duLieuBase64")] data_base64 }`.
Trường vốn đã tiếng Anh (`thumbprint`, `commonName`, `subject`) thì `rename_all = "camelCase"` là đủ.

## Comment

Không lạm dụng comment: chỉ viết khi lý do **không đọc ra được từ code** (bẫy dễ sập, lựa chọn trái trực giác như
`RESET_CARD` thay `LEAVE_CARD`), một dòng, ngắn gọn. Không thuật lại code. Lý do dài ghi vào tài liệu kiến trúc này. Khối code minh hoạ trong tài liệu được phép có chú thích tiếng Việt — đó là lời giảng cho người đọc.

## Error handling

- Mỗi layer một `enum` lỗi bằng `thiserror`: `CardError` (card layer, mang mã `SW`), `ServiceError`, `ApiError`.
- `?` để đẩy lỗi lên; **không** `unwrap()`/`expect()` ngoài `main` lúc khởi động và trong test.
- `CardError` → câu tiếng Việt tại **một** chỗ (`impl Display`), không rải chuỗi khắp nơi.
- Lỗi một phần tử trong `ky` **không** làm hỏng cả đợt: trả `loi` riêng cho phần tử đó như bản C#.

## Logging

`tracing`, mức `info` cho mỗi request và mỗi lệnh thẻ, `warn` cho lỗi thẻ. Kiểu `Pin` không có `Debug` thật; dữ liệu
đem ký không bao giờ đi vào macro log. Review nào thấy `{:?}` trên DTO chứa `duLieuBase64` là chặn.

## Dependencies — ghim trong `[workspace.dependencies]`

Tra crates.io ngày 2026-09-25.

| Vai | Crate |
|---|---|
| Chạm thẻ | `pcsc 2.9.0` · (dự phòng) `nusb 0.2.7` |
| Mật mã **họ RustCrypto ổn định** | `rsa 0.9.10` · `sha2 0.10.9` · `sha1 0.10` · `x509-cert 0.2.5` · `der 0.7` · `spki 0.7` · `zeroize 1.9` |
| HTTP | `axum 0.8.9` · `tokio 1.53` · `tower-http 0.7.1` (CORS, `allow_private_network`) |
| HTTPS | `axum-server 0.8.0` (`tls-rustls`) · `rustls 0.23` · `rcgen 0.14.10` |
| macOS | `tao 0.37` · `tray-icon 0.25.1` · `objc2 0.6` · `objc2-app-kit 0.3.2` · `security-framework 3.7.0` |
| Dữ liệu | `serde 1.0` · `serde_json 1.0` · `serde_repr 0.1` · `base64 0.23` · `hex 0.4` · `chrono 0.4` |
| Lỗi · log | `thiserror 2.0` · `tracing 0.1` · `tracing-subscriber 0.3` · `tracing-appender 0.2` |
| Dò phần nén `7A` | `flate2 1.1` — chỉ trong `tools/ks-probe` |

⚠️ **Đừng ghép `rsa 0.9` với `x509-cert 0.3`/`der 0.8`/`sha2 0.11`.** `rsa 0.9.10` phụ thuộc `digest 0.10`,
`spki 0.7`, `pkcs1 0.7`; `x509-cert 0.3.0` kéo `der 0.8`, `spki 0.8`, `digest 0.11` (kiểm trên crates.io
2026-09-25). Trộn hai họ là lỗi trait không khớp khó đọc khi đưa khoá công khai từ chứng thư sang `rsa`. Bảng crate
ở [../05-chuan-bi-rust.md](../05-chuan-bi-rust.md) (ghi `der 0.8 · x509-cert 0.3`) lệch đúng chỗ này — theo bảng ở
đây.

## Forbidden

Kế thừa nguyên mục "Giới hạn không được vượt" của [README gốc](../../../README.md), trừ đúng một điểm D1 đã
duyệt (PIN đi qua hộp của plugin, sống trong `Zeroizing`, xoá ngay sau `VERIFY`). Còn lại giữ nguyên:

- Không lưu PIN ở bất kỳ dạng nào, không cache PIN giữa hai lần mở phiên.
- Không ghi danh sách chứng thư xuống đĩa (ngoại lệ `.cer` công khai của đường dự phòng, xem
  [03-card-layer.md](03-card-layer.md)).
- Không ghi PIN hay dữ liệu đem ký vào log.
- Không tải binary nào từ Internet rồi chạy.
- Không gửi APDU ghi (`UPDATE BINARY`, `CHANGE REFERENCE DATA`, `RESET RETRY COUNTER`, GlobalPlatform).

## Definition of done mỗi bước

`cargo fmt --check` · `cargo clippy --workspace -- -D warnings` · `cargo test --workspace` · `cargo build` — sạch
trên Windows cho phần chạy được trên Windows; phần `cfg(target_os = "macos")` do CI `macos-15` kiểm ở mỗi push.

> **Tiếp:** [07-build-pipeline.md](07-build-pipeline.md) — code trên Windows, build trên CI, test trên Mac.
