# Bước 9 — Developer ID, notarize, phát qua web

> **Bước 9/9** · trước: [08-dong-goi-chay-thu.plan.md](08-dong-goi-chay-thu.plan.md) · mục lục: [README.md](README.md) ·
> Trạng thái: 📝 chờ duyệt · **Chỉ làm khi bước 8 xanh**

## Input

- Kết luận bước 8: đi tiếp.
- Một tài khoản Apple Developer, lấy theo thứ tự thử: xin **miễn phí diện giáo dục** → nhờ **đơn vị đối tác ký hộ**
  (như gói VCTK của Ban Cơ yếu do Mobile-ID ký) → trả **99 USD/năm** (tài khoản cá nhân cũng đủ, như "Ký số đa năng").

## Steps

1. Tạo chứng thư **Developer ID Application** (ký app) và **Developer ID Installer** (ký `.pkg`); lưu vào GitHub
   Secrets, không đưa vào repo.
2. Script đóng gói: `codesign --options runtime --timestamp -s "Developer ID Application: …"` (hardened runtime) thay
   cho ad-hoc; khai entitlements tối thiểu (mạng server loopback; USB nếu dùng `nusb`).
3. `productbuild --sign "Developer ID Installer: …"`, nộp `.pkg` bằng `xcrun notarytool submit --wait`, rồi
   `xcrun stapler staple`.
4. Kiểm: `spctl --assess --type install -v Ky-so-plugin.pkg` ra `accepted`, `source=Notarized Developer ID`.
5. Phát `.pkg` từ backend như bộ cài Windows (route `api/core/plugin/bo-cai` — việc phía backend, ngoài repo này, cần
   bàn cách chọn file theo hệ điều hành).
6. Thử: tải bằng Safari trên máy sạch, bấm đúp `.pkg`, cài, chạy — không qua System Settings.
7. Chốt: build + notarize bằng script, không thao tác tay.

## Expected output

- Người dùng tải từ web, mở, chạy — y như trải nghiệm Windows.
- Script phát hành lặp lại được, ghi trong README của `MacOS/`.

## Điểm cần chú ý

- Hardened runtime có thể chặn thứ trước đó chạy được ở ad-hoc (nạp thư viện động, JIT) — chạy lại golden test và
  một lô nhỏ sau khi ký.
- Tự cập nhật **không** nằm trong bước này; khi làm phải theo luật "không tải binary từ Internet rồi chạy" — chỉ mở
  `.pkg` đã notarize cho người dùng tự cài.
- Mỗi lần nâng bản vẫn phải thêm vào whitelist `Plugin:PhienBanPhuHop` của mọi backend.

> Hết kế hoạch. Quay lại [README.md](README.md).
