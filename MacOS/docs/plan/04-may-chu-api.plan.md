# Bước 4 — HTTP server axum đúng hợp đồng

> **Bước 4/9** · trước: [03-ky-that.plan.md](03-ky-that.plan.md) · mục lục: [README.md](README.md) ·
> Trạng thái: 📝 chờ duyệt

## Input

- Bước 1–3 xong: `CardDriver` đọc cert và ký được trên Windows.
- Kiến trúc: [../architecture/02-threading.md](../architecture/02-threading.md),
  [../architecture/04-api-layer.md](../architecture/04-api-layer.md).
- Plugin C# đang chạy được trên cùng máy để đối chiếu (chạy lần lượt, cùng cổng `17739`).

## Steps

1. `card-worker`: thread riêng giữ `PcscTransport` + session state, nhận `CardCommand` qua `mpsc`, trả qua `oneshot`;
   tự đóng session khi idle 15 phút bằng `recv_timeout`.
2. `card-watcher`: PC/SC context thứ hai, `SCardGetStatusChange`, gửi `CardEvent::Removed`.
3. `ks-plugin-applications`: `CertificateService` (lọc `onlySignable`, sắp xếp như C#), `SigningService` (map lỗi
   từng phần tử thành `loi`), `PluginService::status` (`ten`, `phienBan` từ `CARGO_PKG_VERSION`, `sanSang`).
4. `ks-plugin-api`: sáu route, `ApiError` → envelope, extractor JSON trả envelope khi body hỏng, `HostGuard`,
   `CorsLayer` với `DEFAULT_ORIGINS` + `allow_private_network`, body limit.
5. `PinDialog` trên Windows chỉ là stub đọc PIN từ console **không echo** — đủ để chạy thử; bản thật ở bước 7.
6. **Golden test**: cùng bộ lệnh `curl` gọi plugin C# rồi plugin Rust trên cùng token, lưu JSON, so từng trường
   (bỏ qua `keyProvider`, `storeDiagnostics`, `source`). Lưu bộ `curl` vào `MacOS/ks-plugin/tests/contract/`.
7. Chạy **màn ký số thật** của FE (`ksts` hoặc `kssm`) trên Chrome với plugin Rust: chọn cert → mở phiên → ký một lô
   nhỏ (≥ 20 file) → đóng phiên. PDF ra verify hợp lệ.
8. Rút token giữa lô ⇒ session đóng ngay, lượt `ky` kế tiếp trả lỗi rõ "Token đã rút", không phải một loạt lỗi mơ hồ.
9. Chốt: `cargo fmt --check` · `clippy -D warnings` · `cargo test --workspace` · `cargo build` sạch trên Windows.

## Expected output

- Lô thật ký xong qua plugin Rust trên Windows, FE không sửa dòng nào.
- Bảng so golden JSON: mọi trường của hợp đồng trùng.
- Log không có PIN, không có `duLieuBase64`.

## Điểm cần chú ý

- HTTP trần ở bước này (Windows, Chrome) — HTTPS để bước 6, vì Windows không phải đích phát hành.
- Hai tab cùng gọi `ky`: phải tuần tự qua `card-worker`, không lỗi, không xen chữ ký nhầm `yeuCauId`.
- Dừng plugin C# trước khi chạy bản Rust — cùng cổng.
- ⚠️ ASP.NET đọc tên trường không phân biệt hoa thường, serde thì có: golden test gửi **đúng** payload FE đang gửi.

> **Tiếp:** [05-len-macos.plan.md](05-len-macos.plan.md) — bản app đầu tiên mang sang Mac.
