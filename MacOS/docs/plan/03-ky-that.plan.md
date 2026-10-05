# Bước 3 — `VERIFY` và ký thật

> **Bước 3/9** · trước: [02-chung-thu.plan.md](02-chung-thu.plan.md) · mục lục: [README.md](README.md) ·
> Trạng thái: 📝 chờ duyệt

Qua bước này là chắc ký được chứng thư Ban Cơ yếu bằng Rust. **Mốc quyết định thứ nhất.**

## Input

- D1 đã duyệt (2026-09-25). **Token dự phòng** (không phải token đang dùng ký thật) + PIN của nó.
- Bước 2 xong: có DER hoặc khoá công khai từ PuKDF để tự kiểm chữ ký.
- Máy chủ C# đã dựng `SignedAttributes` thật từ một lô — lấy vài mẫu làm dữ liệu thử.

## Steps

1. Mở danh sách INS cho phép thêm `VERIFY (20)`, `MSE (22)`, `PSO (2A)`. Không gì khác.
2. `remaining_pin_tries`: `VERIFY` không dữ liệu → đọc `63Cx`. Chạy trên token dự phòng, xác nhận **không tốn lượt**
   (hỏi hai lần liên tiếp ra cùng số).
3. Luật chặn: còn ≤ 1 lượt ⇒ từ chối, không gửi `VERIFY`. Kiểm định dạng PIN (4–16 byte, không NUL) trước khi gửi.
4. `verify_pin`: đệm `0xFF` theo AODF, PIN trong `Zeroizing`. `ks-probe` đọc PIN từ console **không echo**; không
   nhận PIN qua tham số dòng lệnh (lọt vào lịch sử shell).
5. `sign_digest_info`: `MSE:SET` (`84 01 10`, thử các mã thuật toán `80 01 xx`), rồi `PSO:CDS`; thử hai biến thể:
   gửi `DigestInfo` đầy đủ và chỉ gửi hash 32 byte. Xử lý chữ ký 384 byte (Le mở rộng hoặc `61xx`).
6. Tự kiểm chữ ký bằng `rsa::RsaPublicKey::verify(Pkcs1v15Sign::new::<Sha256>())`.
7. **Giữ phiên**: `VERIFY` một lần rồi ký 20 lượt liên tiếp trên cùng kết nối — xác nhận thẻ **không** đòi PIN lại
   (loại trừ *user consent*). Đóng bằng `RESET_CARD` rồi ký lại ⇒ phải bị từ chối `6982`.
8. `ks-probe bench --rounds 20` (công cụ dev, **không** phải route API): đo `T` một lượt ký, so với
   thời gian ký mỗi đợt của plugin C# trên cùng token.
9. Chữ ký của một `SignedAttributes` thật lắp vào CMS phía máy chủ ⇒ PDF verify hợp lệ (Adobe hoặc công cụ verify
   của máy chủ).
10. Chốt: `cargo test --workspace` + `clippy` sạch.

## Expected output

- Một PDF ký bằng chữ ký do Rust lấy từ token, verify **hợp lệ**, chuỗi về Root Ban Cơ yếu.
- Bảng đo: `T` Rust vs `T` C#; kết luận có/không *user consent*; mã thuật toán MSE đúng.

## Điểm cần chú ý

- ⚠️ **Sai PIN 3 lần là khoá chết token.** Mọi phép thử `VERIFY` chỉ trên token dự phòng, luôn qua bước hỏi lượt thử.
- ⚠️ Thẻ đòi PIN trước **mỗi** chữ ký ⇒ **dừng, hỏi lại** — "một PIN cho cả lô" không còn giữ được mà không cache PIN.
- Không log PIN, không log `SignedAttributes`. Log chỉ có `SW`, độ dài, thời gian.
- Middleware bit4id trên máy dev có thể chen vào thẻ giữa hai lệnh — bọc `VERIFY` + ký trong một transaction.

> **Tiếp:** [04-may-chu-api.plan.md](04-may-chu-api.plan.md) — sáu route trên axum.
