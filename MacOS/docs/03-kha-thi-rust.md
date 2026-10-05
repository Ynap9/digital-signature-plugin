# Ký chứng thư Ban Cơ yếu bằng Rust trên macOS

> **Phần 3/5** · trước: [02-ky-so-da-nang.md](02-ky-so-da-nang.md) · mục lục: [README.md](README.md)

## Kết luận

| | |
|---|---|
| Về nguyên lý | ✅ **Được** — không thấy rào cản nào không vượt được. Token VGCA là PKCS#15 chuẩn, APDU qua PC/SC giống nhau trên Windows và Mac, và “Ký số đa năng” đã chứng minh Rust + PC/SC ký token trong nước trên Apple Silicon không cần driver hãng |
| Chi phí | ✅ **0 đồng tới bản nội bộ**. Chỉ phát qua web cho trải nghiệm bấm đúp là chạy mới cần Developer ID 99 USD/năm |
| Đã chắc chưa | 🔶 **Chưa ký thật** — còn 3 phép đo, 2 cái làm được ngay trên Windows |

## Bảy mắt xích

| # | Mắt xích | Trạng thái | Bằng chứng | Nếu hỏng — đường miễn phí |
|---|---|---|---|---|
| 1 | macOS nhận đầu đọc `bit4id TokenME EVO v2` | 🔶 chưa đo | Từ Sonoma, driver mặc định của Apple là *class driver*: mọi thiết bị khai `bInterfaceClass = 0x0B` “should work”. Bit4id **không** có trong danh sách driver CCID mã nguồn mở | ① Bật `ifd-ccid` có sẵn trong máy (lệnh dưới) — nhưng nó tra VID:PID nên có thể không ăn. ② USB CCID trực tiếp bằng `nusb`, như `ccid_transport.rs` của “Ký số đa năng” |
| 2 | Đọc PKCS#15 không PIN | ✅ đã đo | Đọc sạch trên Windows, APDU không phụ thuộc hệ điều hành | — |
| 3 | Lấy chứng thư DER nộp máy chủ | ⚠️ vướng nén | EF `0001` là tag `7A`; OpenSC không giải được | ① Dò thuật toán bằng cặp nén/gốc đã có. ② Người dùng nạp `.cer` một lần, plugin đối chiếu khoá công khai với PuKDF `7004` trên thẻ — không khớp thì từ chối |
| 4 | `VERIFY` PIN ref `0x03` | ❌ rủi ro khoá thẻ | Theo ISO 7816-4, `VERIFY` không kèm dữ liệu trả `63Cx` = còn x lượt, không tốn lượt | Token dự phòng; hỏi lượt thử trước, chặn cứng khi còn ≤ 1 |
| 5 | `MSE:SET` key `0x10` → `PSO:CDS` | 🔶 chưa đo | Plugin Windows ký `SignData(SHA-256, PKCS#1 v1.5)` ⇒ bản Rust tự băm SHA-256, dựng `DigestInfo`; RSA 3072 cho chữ ký 384 byte | Làm trên Windows; thử cả biến thể thẻ nhận `DigestInfo` hay chỉ hash; tự kiểm chữ ký bằng khoá công khai |
| 6 | Trình duyệt gọi được plugin | ✅ biết cách | Safari chặn `http://127.0.0.1` từ trang HTTPS. Chrome/Edge cho gọi nhưng từ **bản 141** hỏi quyền **Local Network Access** một lần | HTTPS loopback: `rcgen` + `security add-trusted-cert` (máy hỏi mật khẩu một lần); hướng dẫn bấm “Cho phép” ở Chrome |
| 7 | Chạy trên máy người dùng | ✅ biết cách | Apple Silicon bắt mọi binary có chữ ký; ad-hoc miễn phí, đủ khi file không dính quarantine | Bảng phát hành dưới |

```bash
sudo defaults write /Library/Preferences/com.apple.security.smartcard useIFDCCID -bool yes
defaults read /Library/Preferences/com.apple.security.smartcard.plist useIFDCCID   # 1 = đang dùng ifd-ccid
```

⚠️ **Sequoia có lỗi PC/SC**: với driver của Apple, `SCardConnect` có lúc báo “Card is unresponsive” dù thẻ đã cấp
nguồn; tác giả driver CCID khuyên đổi sang `ifd-ccid`. Driver Apple cũng **không hỗ trợ `SCardControl()`**. Đây là
lý do giữ sẵn đường USB CCID trực tiếp ở mắt xích 1.

## Phát hành không tốn phí Apple

| Đường | Phí | Người dùng gặp gì |
|---|---|---|
| USB · ổ mạng nội bộ · `scp` | 0 | Chạy thẳng — hợp cho giai đoạn thử nội bộ |
| Lệnh cài một dòng `curl … \| sh` | 0 | `curl` không gắn quarantine nên chạy thẳng; phải mở Terminal một lần và tin máy chủ phát script (bắt buộc HTTPS) |
| Homebrew tap riêng | 0 | Chạy thẳng, cập nhật bằng `brew upgrade`, máy phải có Homebrew |
| Tải bằng trình duyệt | 0 | Bị chặn → System Settings → Open Anyway → mật khẩu máy |
| Developer ID + notarize | 99 USD/năm | Y như Windows. “Ký số đa năng” dùng tài khoản **cá nhân**. Xin miễn phí diện giáo dục là đường nên thử trước |

**Quarantine mới là cổng chặn, không phải chữ ký:**

| Đường chuyển sang Mac | Quarantine | App ký ad-hoc chạy thế nào |
|---|---|---|
| USB / ổ cứng ngoài | không | Chạy thẳng, không hỏi gì — dùng cho bản nội bộ |
| `scp` / `rsync` / SSH | không | Chạy thẳng |
| Homebrew cask | tự gỡ | Chạy thẳng, nhưng người dùng phải biết Terminal |
| Tải bằng Safari / Chrome | **có** | Bị chặn → System Settings → Privacy & Security → *Open Anyway* → mật khẩu máy |
| Mail · Messages · AirDrop | **có** | Như trên |

macOS gắn `com.apple.quarantine` do **trình duyệt/Mail/AirDrop** gắn, máy chủ không tắt được. macOS 15 bỏ cách
Control-click → Open; có báo cáo ở 15.1 app **hoàn toàn không ký** thì không hiện nút Open Anyway — ad-hoc vẫn cứu
được. Gỡ tay: `xattr -dr com.apple.quarantine /Applications/<App>.app`.

⚠️ **Cần một máy Mac để build.** Rust không liên kết binary macOS từ Windows theo đường chính thống (cần SDK Apple).
Mượn máy Mac hoặc runner macOS của GitHub Actions (miễn phí với repo công khai; repo riêng tư tính phút theo hệ số
cao hơn Linux). Driver thẻ thì viết và gỡ lỗi trọn trên Windows.

## Một plugin cho cả Mac Intel lẫn Mac chip M

**Được.** Build `x86_64-apple-darwin` và `aarch64-apple-darwin`, ghép bằng `lipo` thành **universal binary** trong
một `.app`: máy nào chạy bản gốc của chip đó, không qua Rosetta, người dùng tải một file. Giới hạn thật là
**phiên bản macOS**, không phải chip:

| | Mac Intel | Mac chip M |
|---|---|---|
| Rust tối thiểu | macOS 10.12 | macOS 11 (máy M đời đầu xuất xưởng với 11) |
| Bộ crate định dùng (khay, giao diện, TLS) | thực tế ~10.15–11 | 11 |
| “Ký số đa năng” chọn | 12.0 — và chỉ phát bản Apple Silicon | 12.0 |
| **Đề xuất KSTS** | **`LSMinimumSystemVersion = 11.0`** | cùng |

macOS 11 phủ mọi Mac chip M và phần lớn Mac Intel từ ~2014–2015. Máy Intel kẹt ở 10.13–10.15 thì bỏ — hạ mức tốn
công kiểm thử mà ít người dùng.

- ⚠️ **Đầu đọc trên macOS 11–13**: class driver của Apple chỉ có từ **macOS 14**; bản cũ dùng `ifd-ccid` tra VID:PID,
  không có Bit4id ⇒ token có thể không hiện. Nhắm cả 11–13 thì **đường `nusb` là bắt buộc**, không còn là dự phòng.
- ⚠️ **Rust 1.90 (08/2025) hạ `x86_64-apple-darwin` xuống Tier 2** vì GitHub bỏ runner Intel miễn phí. Vẫn build và
  rustup vẫn phát, nhưng lỗi riêng Intel ít được bắt hơn — phải thử trên máy Intel thật.
- ⚠️ **Build một nơi, thử hai nơi**: một Mac bất kỳ build được cả hai bản, nhưng chữ ký ad-hoc có thể chạy trên máy
  build mà hỏng trên máy khác cùng loại — mỗi bản phát hành chạy thử trên cả Intel lẫn chip M.

## Nguồn

- blog.apdu.fr — *Apple's own CCID driver in Sonoma* (2023-11), *macOS Sequoia and smart cards status* (2024-10)
- ccid.apdu.fr — danh sách đầu đọc của driver CCID (không có Bit4id)
- OpenSC `src/libopensc/pkcs15-cert.c` — đọc DER trần, không giải nén
- developer.chrome.com — *New permission prompt for Local Network Access*
- blog.rust-lang.org — *Increasing the minimum supported Apple platform versions* (2023-09), *Demoting
  x86_64-apple-darwin to Tier 2 with host tools* (2025-08)

> **Tiếp:** [04-phuong-an.md](04-phuong-an.md) — bốn phương án .NET/Rust và lộ trình.
