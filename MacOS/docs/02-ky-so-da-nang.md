# Mổ “Ký số đa năng” — Rust ký token trên macOS

> **Phần 2/5** · trước: [01-token-vgca.md](01-token-vgca.md) · mục lục: [README.md](README.md)

Nguồn: `~/Downloads/Ky-so-da-nang-macOS-Apple-Silicon.dmg` (11,3 MB). Bung UDIF/zlib ra ảnh HFS+ 26 MB rồi trích
**đường dẫn mã nguồn trình biên dịch Rust để lại trong thông báo lỗi** — mỗi đường dẫn mang tên crate kèm đúng
phiên bản. Là **bản thiết kế mẫu để học**, không phải linh kiện dùng lại: không có chuỗi `vgca`/Ban Cơ yếu nào.

## Danh tính bản build

| Hạng mục | Đo được |
|---|---|
| Bundle | `vn.flygo.magic-sign`, exe `magic_sign`, phiên bản theo ngày `2026.08.28.162539`, `LSMinimumSystemVersion 12.0`, **không** `LSUIElement` |
| Chữ ký mã | **Developer ID Application: Ba DUong (3LMA9TXC7Z)** — tài khoản Apple Developer **cá nhân** |
| Build | arm64 gốc, rustc commit `59807616…`, **153 crate** |
| Chạm thẻ | PC/SC trực tiếp (`SCardEstablishContext/Connect/Transmit/GetStatusChange`), không middleware hãng |
| Driver thẻ | `drivers/registry.rs` tra ATR; `--token-driver auto\|misa\|fpt`; chuỗi driver là Feitian ePass2003 |
| Tự khởi động | Không thấy `LaunchAgents`/`RunAtLoad`/`SMAppService` — nhiều khả năng người dùng tự mở |
| Cập nhật | `signer.flygo.vn/downloads/release.json`, phiên bản `YYYY.MM.DD.N`, chặn URL ngoài miền tin cậy, tải `.part` rồi `/usr/bin/open` file `.dmg` |
| PKCS#11 | Tự phát hành `libvietnam_usb_token_pkcs11.dylib` làm *provider* cho app khác, đăng ký qua `~/.config/pkcs11/modules/magic-sign.module` |
| Framework liên kết | `PCSC` · `Security` · `CryptoKit` · `LocalAuthentication` (Touch ID) · `AppKit` |
| Tin cậy TLS | Nhúng `Resources/certs/epayment247-localhost/`, cài root vào `login.keychain-db` qua `SecTrustSettings*` |

Chuỗi trợ giúp trong binary ghi thẳng: *“The API binds only to 127.0.0.1. WSS is enabled by default with the
bundled localhost [TLS]”* — ship chứng thư loopback rồi cài root vào login keychain, cách duy nhất để Safari gọi
được. Nó giả lập localhost endpoint của mọi plugin đang lưu hành để website có sẵn chạy luôn:

```text
MISA            ws://localhost:<port>/plugin
Dịch vụ công    ws://127.0.0.1:8768/plugin/sign
VNPT            wss://localhost:4433/plugin
EPayment247     wss://localhost:4000-4003/cmcplugin
CTSigningHub    http://127.0.0.1:32001/api/hubclient
EFY-eBHXH       ws://127.0.0.1:7889/
XML/XAdES       http://127.0.0.1:20005/v1/sign
PKCS#11 bridge  cổng 20003
ECUS5 VNACCS    net.tcp://127.0.0.1:20006/CKSService   (chỉ Windows)
```

## Bố cục workspace

```text
crates/misa-token/        ccid_transport.rs · protocol.rs · certificate.rs · lib.rs
crates/document-signing/  cms.rs · pdf.rs
crates/pkcs11-provider/   agent_confirmation.rs · backend/agent.rs · lib.rs
binary chính              drivers/registry.rs · driver_adapter.rs · signer.rs · token_monitor.rs
                          trust_settings.rs · tray.rs · ui.rs · updater.rs · remote/ · servers/{32001,7889,8768}/
                          integrations/{misa_legacy, meinvoice, minvoice, vnpt, epayment247, ct_hub, efy_com_vn,
                                        vss_bhxh, dvc, ecus, pkcs11}
```

Lõi chạm thẻ (`signer.rs`) tách khỏi lớp giao thức — đúng hình dạng KSTS cần.

## Crate họ dùng

| Vai trò | Họ dùng | KSTS cần? |
|---|---|---|
| PC/SC | `pcsc 2.9.0` | **có** |
| USB CCID trực tiếp | `nusb 0.2.5` (IOKit) + `ccid_transport.rs` tự viết | dự phòng khi macOS không nhận đầu đọc |
| HTTP · HTTPS | `axum 0.8.9` · `axum-server 0.8.0` · `rustls 0.23.42` · `ring 0.17` · `tokio 1.53.0` | **có** |
| Chứng thư loopback | `rcgen 0.14.8` | **có** |
| ASN.1 / X.509 | `x509-parser 0.18` · `der 0.7` · `yasna` · `der-parser 10` | **có** |
| Kiểm chữ ký RSA | `rsa 0.9.10` | **có** — tự kiểm chữ ký thẻ trả về |
| Giao diện | `eframe/egui 0.31` (OpenGL qua `glow`) · `winit 0.30` | có — hộp PIN, hộp xác nhận |
| Khay · keychain | `tray-icon 0.24.1` · `muda 0.19` · `security-framework 3.7.0` · `objc2 0.6` | **có** |
| WebSocket · SQLite · HTTP client | `tokio-tungstenite` · `rusqlite 0.37` · `reqwest 0.12` | không / chưa cần |
| PDF · CMS | `lopdf 0.39` · `document-signing` | **không** — máy chủ KSTS dựng CMS |

## Luồng chạm thẻ, dựng lại từ thông báo lỗi

1. **Nhận diện**: `SCardListReaders` → ATR → `registry.rs` chọn driver; cả chuỗi lệnh bọc trong
   `SCardBeginTransaction`. UI: “Đang khởi động PC/SC” → “Đang nhận diện USB Token” → “Chưa cắm token”.
2. **Đọc chứng thư, không PIN**: `SELECT FILE` · `READ BINARY` · `GET DATA(0186)`; ghép khoá với chứng thư theo ID.
3. **Hỏi lượt thử PIN trước khi nhập**: “PIN retry query”, còn ít thì **“refusing PIN authentication with N tries
   remaining”**. PIN 4–16 byte, cấm NUL, đệm ISO 7816.
4. **Kênh an toàn (chỉ thẻ MISA)**: GlobalPlatform SCP (`INITIALIZE UPDATE` → `EXTERNAL AUTHENTICATE`, AES-CBC), PIN
   gửi đã mã hoá. Token VGCA đọc trần được nên KSTS không bắt buộc phần này.
5. **Ký**: `MANAGE SECURITY ENVIRONMENT` → `PERFORM SECURITY OPERATION`; chỉ SHA-256 (SHA-1 cho VNPT cũ),
   `RSASSA-PKCS1-v1_5`. Sau ký: kiểm độ dài, **tự kiểm chữ ký bằng khoá công khai**, kiểm chứng thư không đổi
   giữa chừng (“USB token changed certificate during signing”).
6. **Canh rút token**: luồng `pcsc-token-watcher` với **PC/SC context riêng**, chờ `SCardGetStatusChange` — bằng sự
   kiện, không quét định kỳ.

## PIN đi qua tiến trình plugin

⚠️ Khác hẳn bản Windows: ở đó middleware bit4id tự bật hộp PIN nên PIN không vào tiến trình plugin (§4 của
[../../Window/docs/bao-mat-agent-ky-so.md](../../Window/docs/bao-mat-agent-ky-so.md)). Tự nói PC/SC thì **không còn ai vẽ hộp PIN hộ**. Họ dùng cửa
sổ egui riêng: “PIN chỉ ở trong Magic Sign; ứng dụng gọi không nhận được PIN”, ký tại máy **hỏi lại mỗi lần**,
chỉ giữ PIN trong RAM khi bật chia sẻ từ xa. Làm bản Mac thì phải sửa dòng này trong
[../../Window/docs/plugin-ky-so.contract.md](../../Window/docs/plugin-ky-so.contract.md).

Hướng giữ “hỏi PIN một lần cho cả lô”: `VERIFY` một lần ở `mo-phien`, xoá PIN khỏi bộ nhớ ngay, **giữ kết nối
PC/SC mở** — trạng thái đã xác thực của thẻ sống tới khi rút/reset, tức là “giữ handle”. ⚠️ Phải đo trên token dự
phòng: khoá mang thuộc tính *user consent* thì thẻ đòi `VERIFY` trước **mỗi** chữ ký.

## Hàng rào cho API localhost

Hơn hẳn chỗ KSTS đang chỉ dựa vào CORS:

- **Hỏi khi website kết nối lần đầu** (“Cho phép website kết nối?”), quyền theo origin kèm **thời hạn và số lượt
  ký**, lưu SQLite.
- **Hộp xác nhận ký**: Website · Ứng dụng · USB Token · Nội dung · SHA-256; nút ĐỒNG Ý KÝ / TỪ CHỐI; **tự hết hạn**.
- **Origin**: đúng một header, dạng `scheme://host[:port]`, chuẩn hoá rồi so danh sách cho phép của từng endpoint.
- **Host**: “requires the exact loopback Host” — chặn DNS rebinding, thứ CORS không che.
- **Mỗi lúc một yêu cầu** (“Một yêu cầu ký khác đang được xử lý”); khung WebSocket ≤ 16 KiB.
- **Cầu PKCS#11** cổng 20003: khoá bí mật trong file (cấm symlink), header `x-magic-sign-nonce`/`timestamp`, phản hồi
  ký HMAC.

## TLS loopback và tin cậy

Sinh chứng thư bằng `rcgen`, phục vụ qua `axum-server` + `rustls`, bind cả IPv4 lẫn IPv6. Cài tin cậy bằng lệnh
hệ thống, rồi đọc lại bằng `SecTrustSettingsCopyTrustSettings`; chưa tin thì báo “certificate imported but
localhost chain is still not trusted”.

```bash
security add-trusted-cert -r trustRoot -k ~/Library/Keychains/login.keychain-db <root.crt>
```

## Học gì, bỏ gì

- **Học**: hỏi lượt thử PIN trước `VERIFY` · luồng canh rút token với context riêng · tự kiểm chữ ký thẻ trả về ·
  kiểm Host ngoài Origin · hỏi quyền theo website · đọc lại trust sau khi cài · Developer ID cá nhân là đủ.
- **Bỏ**: 11 bộ tích hợp, WebSocket, PDF/CMS phía máy, egui trên OpenGL cho một hộp PIN. Họ cần 153 crate vì phục
  vụ mọi website trong nước; plugin KSTS chỉ cần khoảng 20.

> **Tiếp:** [03-kha-thi-rust.md](03-kha-thi-rust.md) — ký chứng thư Ban Cơ yếu bằng Rust trên Mac được không.
