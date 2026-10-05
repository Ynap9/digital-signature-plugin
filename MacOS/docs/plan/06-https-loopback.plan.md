# Bước 6 — HTTPS loopback và Safari

> **Bước 6/9** · trước: [05-len-macos.plan.md](05-len-macos.plan.md) · mục lục: [README.md](README.md) ·
> Trạng thái: 📝 chờ duyệt

## Input

- D2 đã duyệt (2026-09-25): bản Mac chỉ HTTPS trên `17739`; FE dò `https://` trước, trượt thì `http://`.
- Bước 5 xanh: server chạy trên Mac bằng HTTP.
- Kiến trúc `LoopbackTls`: [../architecture/05-macos-platform.md](../architecture/05-macos-platform.md).

## Steps

1. `LoopbackTls::ensure()`: sinh CA riêng của máy (Name Constraints `localhost`, `127.0.0.1`, `::1`), ký leaf 825 ngày,
   **huỷ khoá CA**, lưu leaf key `0600`.
2. Cài tin cậy root vào login keychain bằng `security add-trusted-cert`, đọc lại bằng
   `SecTrustSettingsCopyTrustSettings`; chưa tin ⇒ báo lỗi rõ, không chạy tiếp như không có gì.
3. Đổi listener sang `axum-server` + `rustls`, bind `127.0.0.1` và `::1`.
4. Leaf gần hết hạn (< 30 ngày) ⇒ sinh lại cả cặp, cài tin cậy lại, gỡ root cũ khỏi keychain.
5. Việc phía FE (ngoài repo này, ghi để bàn giao): phép dò `trang-thai` thử `https://127.0.0.1:17739` trước, timeout
   ngắn, trượt thì `http://`; nhớ scheme đã dò được cho các lời gọi sau trong phiên trang.
6. Thử trình duyệt: Safari, Chrome (hộp **Local Network Access** từ bản 141 — bấm "Cho phép" một lần), Firefox (kho
   gốc riêng — thử `security.enterprise_roots.enabled`), trên Mac chip M, macOS 14+ (Intel nếu có).
7. Chốt: `cargo test` (test đơn vị cho sinh cert: SAN đúng, CA key không còn trên đĩa, quyền file `0600`) + build sạch.

## Expected output

- Bảng: trình duyệt × (gọi được `trang-thai`? · cần thao tác gì của người dùng?).
- Màn ký số thật trên **Safari** ký xong một lô nhỏ.
- Hướng dẫn người dùng một trang: mật khẩu keychain lần đầu, bấm "Cho phép" ở Chrome.

## Điểm cần chú ý

- ⚠️ **Không** ship cặp khoá dùng chung, **không** giữ khoá CA — root được tin kèm khoá trên đĩa là cho mã độc tự cấp
  cert cho mọi tên miền.
- `security add-trusted-cert` hỏi mật khẩu ⇒ chỉ gọi ở lần chạy đầu hoặc khi gia hạn, không gọi mỗi lần khởi động.
- Đổi scheme **không** đổi route hay JSON — hợp đồng còn nguyên ngoài chữ `https`.

> **Tiếp:** [07-vo-macos.plan.md](07-vo-macos.plan.md) — menu bar, hộp PIN, tự khởi động.
