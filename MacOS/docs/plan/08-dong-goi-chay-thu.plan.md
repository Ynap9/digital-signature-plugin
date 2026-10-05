# Bước 8 — Trình cài đặt đầy đủ, dùng thử lô thật

> **Bước 8/9** · trước: [07-vo-macos.plan.md](07-vo-macos.plan.md) · mục lục: [README.md](README.md) ·
> Trạng thái: 📝 chờ duyệt

**Mốc quyết định thứ hai**: có đáng đầu tư tiếp (Developer ID, phát qua web) hay không. Tới hết bước này **chưa tốn
phí Apple nào**.

## Input

- Bước 7 xanh trên Mac chip M (và Mac Intel nếu có).
- Một người dùng nội bộ có token thật và quyền ký lô thật trên `ksts` hoặc `kssm`.
- Bộ cài Windows làm mẫu: `Window/ks.plugin/bo-cai.nsi`; bảng ánh xạ ở
  [../architecture/07-build-pipeline.md](../architecture/07-build-pipeline.md).

## Steps

1. `distribution.xml` đủ các trang như `bo-cai.nsi`: **Giới thiệu** (cùng lời văn), **Nơi cài** (`~/Applications`),
   **Tuỳ chỉnh** — chọn môi trường Production / Staging / Development (ba choice loại trừ nhau, mặc định Production),
   **Cài đặt**, **Tóm tắt** ("Quay lại trang ký số và bấm Kiểm tra lại"). Tiêu đề, icon, tiếng Việt.
2. `preinstall`: có bản đang chạy ⇒ yêu cầu thoát (đóng session), đợi nhả `SingleInstanceLock` — tương ứng `taskkill`.
3. `postinstall`: ghi `config.json` (`installDir`, `environment`), ghi LaunchAgent, `launchctl bootstrap gui/<uid>`, mở
   app — tất cả **dưới quyền người dùng đang ngồi máy**.
4. App: `InstallConfig` đọc `config.json`; menu "Gỡ cài đặt" chạy `uninstall.sh` (tắt LaunchAgent, gỡ root loopback,
   xoá `Application Support`, `pkgutil --forget`, chuyển app vào Thùng rác) — tương ứng `go-cai-dat.exe`.
5. Script CI `packaging/macos/build.sh` dựng trọn: build hai kiến trúc → `lipo` → `.app` → ad-hoc sign **sau cùng** →
   các gói con → `.pkg`. Kiểm: `lipo -info` ra `arm64 x86_64`, `codesign --verify --deep --strict`,
   `pkgutil --check-signature` (ra "no signature" là đúng ở bước này), `plutil -lint`.
6. Thêm phiên bản mới vào whitelist `Plugin:PhienBanPhuHop` của **mọi** backend dùng plugin.
7. Thử cài trên Mac: cài mới · cài đè bản cũ (cập nhật) · chọn Staging rồi kiểm `config.json` · "Gỡ cài đặt" sạch.
8. Chạy **một lô thật** trên Safari và Chrome: chọn cert → nhập PIN một lần → ký hết lô. Đo `T` bằng log thời gian ký
   (không có route đo tốc độ), so với số trên Windows.

## Expected output

- `Ky-so-plugin.pkg`: bấm đúp → wizard 5 trang như bộ cài Windows → app chạy nền trên menu bar, tự khởi động.
- Biên bản lô thật: số file, thời gian, `T`, lỗi gặp, trình duyệt. Kết luận **đi tiếp bước 9 hay dừng**.

## Điểm cần chú ý

- Khác Windows không tránh được: không chọn thư mục cài tuỳ ý, không có ô "Chạy ngay" ở trang cuối (app luôn mở),
  không có Apps & Features. Không có bước cài middleware — bản Mac không cần.
- ⚠️ Chữ ký ad-hoc có thể chạy trên runner mà hỏng trên máy khác — luôn cài thử trên Mac đích.
- `.pkg` chưa ký tải bằng Safari sẽ bị chặn; giai đoạn này **chỉ** chuyển bằng USB.

> **Tiếp:** [09-phat-hanh.plan.md](09-phat-hanh.plan.md) — Developer ID và notarize.
