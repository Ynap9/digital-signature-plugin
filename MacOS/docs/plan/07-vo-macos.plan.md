# Bước 7 — macOS platform: menu bar, hộp PIN, tự khởi động

> **Bước 7/9** · trước: [06-https-loopback.plan.md](06-https-loopback.plan.md) · mục lục: [README.md](README.md) ·
> Trạng thái: 📝 chờ duyệt

## Input

- D1 đã duyệt (tự dựng UI hộp PIN); D5 đã chốt: app `Ký số plugin.app`, bundle id `vn.ynap.kysoplugin`.
- Bước 6 xanh. Kiến trúc: [../architecture/05-macos-platform.md](../architecture/05-macos-platform.md),
  [../architecture/02-threading.md](../architecture/02-threading.md).

## Steps

1. `app/main.rs` theo đúng **startup flow**: `SingleInstanceLock` → `LoopbackTls::ensure` → spawn `card-worker`,
   `card-watcher`, tokio thread → event loop `tao` trên `main` thread.
2. `MenuBar` (`tray-icon` + `muda`): trạng thái (đang nghe / session mở với CN nào / token đã rút), "Đóng phiên ký",
   "Mở thư mục log", "Tự khởi động" (bật/tắt), "Thoát".
3. `PinDialog` (`objc2-app-kit`), UI tự dựng: `NSPanel` floating + `NSSecureTextField`, `NSApp.activate` trước `runModal`; hiện tên token, CN,
   số lượt thử còn, Origin xin mở phiên. Nhận `PinRequest` qua `EventLoopProxy`, trả `Option<Pin>` qua `oneshot`.
4. `Pin`: `Zeroizing<Vec<u8>>`, `impl Debug` in `"***"`, không `Clone`, không `Serialize`.
5. `LaunchAgentInstaller`: ghi/xoá `~/Library/LaunchAgents/vn.ynap.kysoplugin.plist` (`RunAtLoad`,
   `KeepAlive.SuccessfulExit = false`) theo lựa chọn ở menu và lần chạy đầu.
6. `CardEvent::Removed` ⇒ đóng session + thông báo trên menu bar.
7. Logging: `tracing-appender` ra `~/Library/Logs/KySoPlugin/`, xoay theo ngày, giữ 14 file.
8. Graceful shutdown khi bấm "Thoát": tắt axum, `CloseSession`, rồi thoát event loop.
9. Menu "Xuất báo cáo chẩn đoán" (mở rộng từ bước 5) thêm trạng thái session, LaunchAgent, cert loopback.
10. Chốt: CI xanh, tải `.pkg` về Windows, chép USB, cài và dùng thử trên Mac, gửi lại báo cáo chẩn đoán.

## Expected output

- App không có icon Dock, có icon menu bar; khởi động lại máy thì tự chạy.
- Hộp PIN nổi **trên** Safari/Chrome, hiện số lượt thử còn; Huỷ không tốn lượt.
- Rút token giữa lô: menu bar báo, FE nhận lỗi rõ ở lượt `ky` kế tiếp.
- Mở app lần hai: không có bản thứ hai.

## Điểm cần chú ý

- ⚠️ Mọi thứ AppKit **chỉ** trên `main` thread — gọi từ `card-worker` là crash; luôn đi qua `EventLoopProxy`.
- Hộp PIN là chỗ duy nhất PIN tồn tại dạng chuỗi (`NSString`): lấy xong xoá nội dung ô ngay, không giữ tham chiếu.
- Hộp PIN có timeout (vd 2 phút) ⇒ tự đóng, trả `None`; handler không treo mãi.
- Thiếu `LSUIElement` trong `Info.plist` thì icon Dock vẫn hiện dù đặt `ActivationPolicy::Accessory` lúc chạy.

> **Tiếp:** [08-dong-goi-chay-thu.plan.md](08-dong-goi-chay-thu.plan.md) — đóng `.app`, phát nội bộ, chạy lô thật.
