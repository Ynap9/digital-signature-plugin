# Bước 5 — `.pkg` đầu tiên: mang sang Mac, chẩn đoán đầu đọc và thẻ

> **Bước 5/9** · trước: [04-may-chu-api.plan.md](04-may-chu-api.plan.md) · mục lục: [README.md](README.md) ·
> Trạng thái: 📝 chờ duyệt

Lần đầu code chạm Mac thật. Không có Mac để dev nên mục tiêu của bước này là **một bộ cài tối thiểu** đủ để người
mang sang Mac chỉ cần bấm đúp, rồi gửi về một báo cáo chẩn đoán.

## Input

- Bước 1–4 xanh trên Windows; workflow CI macOS (bước 0) đang xanh.
- Một Mac chip M, macOS 14+, để **dùng thử** (không dev trên đó); thêm một Mac Intel đời mới nếu mượn
  được. Token **dự phòng** + đầu đọc `bit4id TokenME EVO v2`.
- Kiến trúc: [../architecture/07-build-pipeline.md](../architecture/07-build-pipeline.md).

## Steps

1. CI: build hai kiến trúc, `lipo` ra universal, dựng `Ký số plugin.app` (`Info.plist` tối thiểu, `LSUIElement`,
   `LSMinimumSystemVersion = 14.0`), `codesign -s -` **sau cùng**, rồi `pkgbuild` + `productbuild` ra `Ky-so-plugin.pkg` với wizard tối thiểu: Giới thiệu → Nơi cài
   (`currentUserHome`) → Cài đặt → Tóm tắt. `postinstall` chỉ mở app — chưa LaunchAgent, chưa chọn môi trường.
2. App ở bước này: HTTP server (tạm HTTP), `card-worker`, `MenuBar` tối thiểu với "Xuất báo cáo chẩn đoán" và "Thoát".
3. Báo cáo chẩn đoán gom: phiên bản macOS, chip, danh sách đầu đọc, ATR, dump PKCS#15 (công khai), kết quả tự gọi
   `trang-thai` và `chung-thu-so`, người chạy `postinstall` (`whoami`), nơi app được cài, log.
4. Tag `macos-v0.1.0` ⇒ `.pkg` lên GitHub Release. Tải về Windows, chép **nguyên file** vào USB.
5. Trên Mac: bấm đúp `.pkg` → cài → cắm token → menu bar "Xuất báo cáo chẩn đoán" → chép `.zip` về USB.
6. Đọc báo cáo trên Windows:
   - đầu đọc có hiện, ATR có ra, dump có khớp fixture bước 1 không;
   - `postinstall` chạy dưới user nào, app nằm ở `~/Applications` hay chỗ khác (kiểm ⚠️ ở kiến trúc 07).
7. Đầu đọc **không** hiện hoặc "Card is unresponsive" (lỗi đã biết trên Sequoia) ⇒ thử lại với hướng dẫn bật `ifd-ccid`
   (`sudo defaults write /Library/Preferences/com.apple.security.smartcard useIFDCCID -bool yes`); vẫn hỏng thì làm
   `CcidTransport` (`nusb`) sau trait `Transport` rồi ra `.pkg` mới.
8. Khi đầu đọc ổn: thêm vào báo cáo một lần **ký thử trên token dự phòng** qua hộp PIN tạm (chưa phải `PinDialog`
   cuối) để xác nhận ký được trên Mac.

## Expected output

- `.pkg` cài được bằng bấm đúp, không cần Terminal, không qua *Open Anyway*.
- Báo cáo: macOS × (đầu đọc hiện? · đọc cert? · ký được?) · transport (`pcsc`/`nusb`) · kết quả domain cài per-user.

## Điểm cần chú ý

- ⚠️ Chép **file `.pkg`**, không chép thư mục `.app` qua Windows — NTFS/exFAT làm mất bit thực thi.
- Mỗi vòng thử là một lượt đi-về bằng USB, tốn thời gian ⇒ báo cáo chẩn đoán phải đủ để không cần hỏi lại.
- Không cài VGCA VCTKManager hay middleware bit4id lên Mac thử — phải chứng minh chạy **không** cần driver hãng.

> **Tiếp:** [06-https-loopback.plan.md](06-https-loopback.plan.md) — HTTPS loopback cho Safari.
