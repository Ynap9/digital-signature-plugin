# Plan — Plugin ký số ở máy người dùng

> **Trạng thái: 🔶 phần ký đã chạy thật, phần bảo mật nâng cao để tối ưu sau** (cập nhật 2026-10-05).
> Hợp đồng đang chạy: [plugin-ky-so.contract.md](plugin-ky-so.contract.md).
> Nền: [luong-ky-so-hang-loat.md](luong-ky-so-hang-loat.md). Mã nguồn [`../ks.plugin/`](../ks.plugin/README.md).

## Hình dạng đang chạy

Plugin là **web API nghe `http://127.0.0.1:17739`** (topology A). Trang web gọi thẳng vào đó và làm **người
đưa thư**: lấy `SignedAttributes` từ máy chủ, đưa xuống plugin ký, mang chữ ký thô trả về. Plugin **không**
biết gì về tài khoản, **không** nhận file nào, **không** gọi ra máy chủ.

```
BE  <--HTTPS-->  Trang web  <--http://127.0.0.1-->  Plugin  -->  Token
```

## Đã làm

| # | Việc | Kết quả |
|---|---|---|
| 1 | `ICertificateProvider` — liệt kê chứng thư, **không** hỏi PIN | ✅ |
| 2 | `ITokenVerifier` — ký thử mẩu ngẫu nhiên, trả đúng một cờ `valid` + `reason` | ✅ |
| 3 | `ISigningSession` — `Open(thumbprint)` giữ handle khoá, `Sign(dữ liệu)`, `Close()` | ✅ |
| 4 | Phiên tự đóng sau **15 phút** không dùng (`KySoConstants.IdleTimeoutMinutes`), bằng bộ hẹn giờ nền chứ không chờ lượt ký kế | ✅ |
| 5 | `ky-so/ky` nhận **cả một đợt** yêu cầu, hỏng cái nào trả `loi` riêng cái đó | ✅ |
| 6 | Bỏ route `ky-so/do-toc-do` (2026-10-05) — chạm token không phục vụ người ký, gọi giữa lô là đóng ngang phiên | ✅ |
| 7 | Bộ cài một file exe, nhúng sẵn middleware **bit4id** và **TokenManager** (token trước 2022, 2026-10-05), cài per-user | ✅ chưa thử `/S` TokenManager trên máy sạch |
| 8 | Đổi tên `KstsPlugin` → `KySoPlugin`: plugin dùng chung cho nhiều backend, không còn thuộc riêng KSTS | ✅ |
| 9 | `dong-goi.ps1` xuất bộ cài sang `Plugins/` của **cả** `ksts.be` lẫn `kssm.be` | ✅ |
| 10 | `GET api/core/plugin/phien-ban` ở cả hai BE — whitelist `Plugin:PhienBanPhuHop` trong `appsettings.json` | ✅ |
| 11 | Exe đổi tên `Ký số plugin.exe`, phiên bản đọc từ assembly, CORS thêm `localhost:3000`, FE gọi `phien-ban` | ✅ |
| 12 | Giám sát rút token: quét cert store mỗi 2s (`SessionCheckSeconds`), cert biến mất ⇒ đóng phiên, `ky` trả lý do rõ | ✅ chưa thử token thật |
| 13 | `KeyProviders` nhận cả khoá CSP kiểu cũ (`RSACryptoServiceProvider`) — token SafeNet không còn bị đánh là khoá phần mềm | ✅ chưa thử token thật |
| 14 | Exe `WinExe`, bỏ console; `StatusWindow` hiện trạng thái + nhật ký (RAM), icon ký số mới | ✅ |
| 15 | Phiên bản `3.0.0` theo SemVer — MAJOR vì bỏ route `ky-so/do-toc-do`; whitelist cả hai BE chỉ nhận `3.0.0` (2026-10-05) | ✅ |
| — | Pairing one-time token, job ticket, WYSIWYS, consent dialog | 🔬 chưa — xem cuối |

Đổi tên đụng cả thư mục cài, khoá autostart và khoá gỡ cài đặt — máy đã cài bản `KstsPlugin` phải gỡ tay một
lần, xem ⚠️ trong contract. Phiên bản là **một chuỗi khớp chính xác**, không so lớn-bé; nay đọc từ assembly
nên `<Version>` trong csproj là nguồn duy nhất, nâng bản thì chỉ còn phải thêm vào whitelist của mọi backend.

**PIN bật đúng một lần cho cả lô** ở `ky-so/mo-phien`: mở khoá rồi GIỮ handle. Giữ handle **khác** cache PIN —
PIN vẫn đi thẳng từ bàn phím vào middleware qua CNG/minidriver, không byte nào vào tiến trình plugin.

Plugin đọc chứng thư qua **Windows certificate store** (`X509Store`), kể cả cert nằm trên token — middleware
bit4id hoặc TokenManager tự bắc cầu khoá vào store. **Không** nạp `.dll` PKCS#11 nào. Đổi lại, máy **phải cài middleware** thì
token mới hiện trong store, đó là lý do bộ cài gói cả hai vào một exe.

## Điểm cần chú ý (vẫn đúng)

- **Không tự vẽ ô nhập PIN.** Để middleware bật qua CNG; PIN không vào process. Phải kiểm cấu hình middleware
  vì nó có thể tự cache PIN theo cách riêng.
- Hộp PIN có thể hiện chìm sau trình duyệt. SIPPACK set thuộc tính CNG `"HWND Handle"`; plugin nay đã có
  `StatusWindow` ẩn để làm HWND nhưng chưa truyền vào CNG. **Chưa kiểm trên máy thật có token.**
- **Không cache danh sách cert xuống đĩa** — enumerate lại mỗi lần.
- Cài **per-user** `%LocalAppData%`, autostart `HKCU\...\Run`, không driver, không service SYSTEM ⇒ không UAC.
- **Code signing (tối thiểu OV)** trước khi rollout thật: binary chưa ký + chạy nền + tự khởi động + đụng
  crypto token + kết nối mạng là chân dung malware với AV; máy cơ quan hay bật *Warn and prevent bypass*.
- CORS ở `ks.plugin.api/appsettings.json` → `Cors:AllowedOrigins` là **điều kiện để chạy**, không phải lớp
  bảo mật: `Origin` chỉ có giá trị với request phát từ browser.

## Còn phải làm ngay

1. **Khai origin prod của FE gọi `kssm.be`** vào `PluginConstants.OriginMacDinh` rồi đóng gói lại — danh sách
   ghim trong mã nên sửa `appsettings.json` của bản phát hành không ăn.
2. **Đo `T`** khi có token thật — thời gian một đợt `ky` chia số yêu cầu; con số quyết định thời lượng cả lô.
3. **Kiểm hộp PIN có hiện chìm không** trên máy thật.
4. **Thử rút token giữa lô trên máy thật**: middleware bit4id có gỡ chứng thư khỏi store khi rút không. Không gỡ
   thì giám sát rút token không ăn, phiên chỉ đóng theo mốc 15 phút.

## 🔬 Nghiên cứu — tối ưu sau

Thiết kế đầy đủ ở [bao-mat-agent-ky-so.md](bao-mat-agent-ky-so.md). Bốn mảnh chưa làm,
xếp theo thứ tự đáng làm trước:

1. **Job ticket** server ký, public key ghim cứng lúc build; kiểm `nonce` chưa dùng, `exp` còn hạn,
   `certThumbprint` khớp cert đang mở, `signedAttrs.Length == opCount`. Chặn T1 + T2 thật sự, thay cho việc
   hiện chỉ dựa vào CORS + phiên do người dùng tự mở.
2. **WYSIWYS** — nhận cả PDF đã prepare, tự tính digest hai dải `/ByteRange` so với `messageDigest`, render
   trang đầu cho người dùng xem. Chặn T3 (server bị chiếm).
3. **Consent dialog native**, OS-modal, topmost: user nào · bao nhiêu file + tên file · chứng thư CN nào.
   Rate limit 3 phiên/phút.
4. **Topology B** — bỏ listener, plugin tự gọi ra server qua WSS. Chỉ cần khi nghiệp vụ đòi **đóng tab mà lô
   vẫn chạy**; phần lõi phía BE (`IHangDoiKy`, `PluginSigningKey`) giữ nguyên, chỉ thay lớp vận chuyển.

Kèm theo là **pairing** (one-time token trong tên file installer) và **gỡ cài đặt báo server unpair** — hai
thứ chỉ có ý nghĩa khi đã có job ticket, vì chúng phục vụ việc server biết plugin nào thuộc về ai.
