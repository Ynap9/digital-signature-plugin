# Thiết kế bảo mật agent ký số

> **Loại tài liệu: NGHIÊN CỨU — tối ưu sau.** Nguồn: session nghiên cứu web ký số trên repo Sip (2026-08).
> Đọc cùng [ky-so-web-vs-desktop.md](ky-so-web-vs-desktop.md) và [luong-ky-so-hang-loat.md](luong-ky-so-hang-loat.md).
>
> Bản chạy thật đi một đường **nhẹ hơn** thiết kế này: plugin nghe loopback, trang web làm người đưa thư, phiên
> ký do chính người dùng mở bằng PIN. Bảng dưới nói rõ mảnh nào đã có, mảnh nào chưa.

## Đã thi công tới đâu

| Mục | Trạng thái |
|---|---|
| §4 PIN không vào process — middleware tự bật dialog | ✅ Đang chạy (`SigningSession` giữ handle khoá) |
| §6 Phiên tự đóng sau 15 phút không dùng | ✅ `KySoConstants.IdleTimeoutMinutes` |
| §6 Đóng phiên khi rút token | ✅ quét cert store mỗi 2s — chưa thử token thật |
| §7 Server tự dựng chain, không tin cờ client gửi | ✅ `ICertificateTrustValidator`, kiểm ở `lo-ky/{id}/mo-phien` |
| §7 Không cache danh sách cert | ✅ Enumerate lại mỗi lần |
| §2 Topology B (plugin gọi ra qua WSS) | ❌ Đang dùng A — xem mục §2 |
| §3 Job ticket server ký + nonce | ❌ Chưa có |
| §5 WYSIWYS (plugin tự tính digest, render trang) | ❌ Chưa có |
| §6 Consent dialog native + rate limit | ❌ Chưa có |
| §8 Pairing bằng one-time token, gỡ cài đặt báo server | ❌ Chưa có |

## 1. Threat model

| # | Kẻ tấn công | Làm được gì | Chặn bằng |
|---|---|---|---|
| T1 | **Website độc hại** ở tab khác | Gọi thẳng agent, xin ký | Job ticket server ký (§3) — **không phải** `Origin` |
| T2 | **Malware trên máy client** | Gọi agent như client hợp lệ | Consent + capability limit. Không chặn triệt để |
| T3 | **Server bị chiếm** | Phát ticket ký nội dung khác | Agent tự tính digest + WYSIWYS (§5) |
| T4 | MITM mạng | Nghe/sửa FE↔server | TLS |
| T5 | User khác cùng máy | Chạm được agent | Session bind theo user |
| T6 | Replay | Dùng lại ticket cũ | nonce một lần + `exp` ngắn |

⚠️ **`Origin` header chỉ có giá trị với request phát từ browser.** `curl`, malware, script Python đặt
`Origin` tuỳ ý. Bảo mật chỉ dựa vào allowlist origin thì chặn được T1 nhưng **hở toàn bộ T2**. Nhiều plugin
ký số VN dừng ở đúng mức này.

**Nguyên tắc gốc: agent không được là signing oracle.** API kiểu `POST /sign {hash} → signature` nghĩa là ai
chạm được cũng ký được mọi thứ; hash là **mù**, agent không biết đang ký gì, người dùng càng không.

## 2. Topology — đang dùng A, B để tối ưu sau

**A — agent mở listener `127.0.0.1`** (cách phổ biến VN) ✅ **đang chạy**: plugin nghe
`http://127.0.0.1:17739`, FE gọi thẳng. Kéo theo Private Network Access của Chrome (đã đổi cơ chế vài lần,
**phải test lại trên đúng bản đang dùng**), xung đột port, và một bề mặt tấn công thường trực. Trang `https://`
gọi `http://127.0.0.1` **không** dính mixed content (`ws://` thì có), nên chỗ này không vướng.

Chọn A vì ít việc hơn hẳn và kịp cho bản chạy thật. Rủi ro T1 hiện chặn bằng **CORS + phiên do người dùng tự
mở bằng PIN**, không phải bằng job ticket — `Origin` không phải hàng rào bảo mật, xem cảnh báo ở §1.

> Tuyệt đối **không** ship cert Let's Encrypt thật cho `local.domain.vn → 127.0.0.1`: private key nằm trong
> mọi bản cài = coi như công khai, vi phạm CA/B Forum và **sẽ bị thu hồi**.

**B — agent chỉ gọi ra, không mở cổng nào** 🔬 **nghiên cứu, tối ưu sau**

```
Browser (FE) ──HTTPS──> Server <──WSS outbound── Agent ──> Token
                        (job queue)
```

FE **không bao giờ nói chuyện với agent**, chỉ poll trạng thái job từ server. Xoá sổ **T1 hoàn toàn** (không
listener → không website nào gọi được), hết mixed content, hết PNA/CORS, hết TLS localhost, hết xung đột
port, hết cảnh báo firewall. Giá phải trả: cần pairing UX + job queue ở server.

Lợi ích nghiệp vụ quyết định việc có làm hay không: **đóng tab mà lô vẫn ký tiếp**. Đường đang chạy không làm
được điều đó. Đổi sang B **không phải viết lại phần lõi** — `IHangDoiKy` và `PluginSigningKey` giữ nguyên, chỉ
thay lớp vận chuyển.

## 3. Job ticket — server ký, agent xác minh 🔬 chưa thi công

Server có keypair riêng (không liên quan chứng thư người dùng); **public key ghim cứng vào agent lúc build**.

```jsonc
{
  "jobId": "…", "nonce": "…", "exp": "…",        // sống 2–5 phút
  "origin": "https://kyso.huce.edu.vn",
  "userId": "…",
  "certThumbprint": "…",                          // khoá chặt vào 1 chứng thư
  "opCount": 137,                                 // khoá chặt số lần ký
  "files": [ { "name": "A.pdf", "sha256": "…" } ],
  "signedAttrs": [ "<DER base64>" ]
}
```

Agent bắt buộc kiểm, thiếu mục nào thì từ chối: ① chữ ký server hợp lệ với public key đã ghim → chặn T1+T2 ·
② `nonce` chưa dùng → chặn T6 · ③ `exp` còn hạn · ④ `certThumbprint` khớp cert user vừa chọn · ⑤
`signedAttrs.Length == opCount` · ⑥ tự bóc `SignedAttributes`: `signingCertificateV2` khớp cert của mình,
`messageDigest` khớp digest agent tự tính.

## 4. PIN — không bao giờ chạm vào

> **Đừng để PIN đi vào process của mình, kể cả trong RAM.**

Trên Windows, để **middleware token (bit4id / VGCA GCA01) tự hiện PIN dialog** qua CNG/minidriver. Ta chỉ ra
lệnh ký; PIN đi thẳng từ bàn phím vào CSP/KSP. **Process của ta không thấy một byte PIN nào.** Đây là cách
`StoreSigningKey` của SIPPACK đang làm.

- **Không tự viết ô nhập PIN.** Tự viết = giữ PIN trong memory, có thể bị dump/keylog/log nhầm.
- **"Kiểm tra PIN đúng không" = thử ký một mẩu dữ liệu test** → trả **đúng boolean**, không kèm số lần thử
  còn lại, không kèm lý do chi tiết. Kết quả này **không** được đi lên server như một chứng nhận.

Hai cạm bẫy: **(a)** middleware có thể tự cache PIN theo cấu hình riêng của nó — code sạch là chưa đủ, phải
kiểm cấu hình middleware. **(b)** phân biệt **cache PIN** với **giữ key handle**: giữ handle một phiên là thứ
khiến N file chỉ hỏi PIN một lần, đó **không phải** cache PIN.

## 5. WYSIWYS — agent tự biết mình ký gì 🔬 chưa thi công

Chống T3. Agent nhận luôn **PDF đã prepare** (có `/ByteRange` placeholder) rồi tự: tính digest hai dải
`/ByteRange` từ bytes thật → so với `messageDigest` trong `SignedAttributes` (lệch là từ chối) → kiểm
`signingCertificateV2` trỏ đúng cert của mình → **render trang đầu cho người dùng xem**. Server có thể nói
dối về *tên* file nhưng không nói dối được về *nội dung* người dùng đang nhìn.

## 6. Session = capability

Gắn đúng 1 `jobId` + 1 `certThumbprint` + 1 `userId`; đúng `opCount` thao tác, hết là chết, **không gia hạn**;
TTL 2–5 phút; kết thúc khi hết TTL / hết opCount / **rút token** / user huỷ / agent thoát → dispose key handle
ngay. Một **consent dialog native, OS-modal, topmost** hiện đúng ba thứ: user nào, bao nhiêu file + tên file,
chứng thư CN nào. Thêm **rate limit** (vd 3 session/phút) chống consent fatigue.

## 7. Không lưu gì

| Thứ | Trạng thái |
|---|---|
| PIN | ❌ Không lưu, không hash, **không nhận vào process** |
| Private key | ❌ Bất khả thi — nằm trong chip |
| Cert cache trên đĩa | ❌ **Enumerate lại mỗi lần** — cache cert là nguồn của cả loạt bug |
| Session token | ❌ Chỉ RAM |
| nonce đã dùng | ⚠️ RAM + TTL |
| Key handle | ⚠️ RAM, đúng một session, dispose tường minh |
| Audit log | ✅ Ghi sự kiện (thời điểm, jobId, thumbprint, số file) — **không** PIN, **không** nội dung file |

> **Server chỉ nhận cert public + chữ ký, và phải TỰ verify chain** về CA đã ghim. **Không tin cờ `IsTrusted`
> do agent gửi lên** — agent chạy trên máy không tin cậy được. Đây là lý do `ICertificateTrustValidator` nằm
> ở BE ngay từ bây giờ.

## 8. Phân phối agent

Cài **per-user** (`%LocalAppData%`), autostart `HKCU\...\Run`, **không cài driver**, **không service SYSTEM**
→ **không có UAC**. Installer chạy silent vài giây, cài xong agent tự chạy.

**Pairing = 0 thao tác**: link tải mang sẵn one-time token trong tên file (`KySoSetup-a3f9c2.exe`), installer
đọc tên file của chính nó, agent tự đăng ký với server. Token dùng một lần, sống ~30 phút, gắn cứng vào user.

**Gỡ được là bắt buộc**: mục trong Apps & Features, icon khay có Thoát + Gỡ cài đặt, khi gỡ thì kill process →
xoá autostart → xoá `%LocalAppData%` → **báo server unpair**.

**SmartScreen**: giai đoạn dev/demo/pilot cứ để user bấm *More info → Run anyway*. Trước khi rollout thật thì
cần code signing (tối thiểu OV) — máy cơ quan thường bật policy *Warn and prevent bypass* (**không có nút Run
anyway**) và AppLocker/WDAC đòi binary có chữ ký; ngoài ra binary chưa ký + chạy nền + tự khởi động + đụng
crypto token + kết nối ra ngoài là chân dung malware sách giáo khoa với AV.

## 9. Rủi ro tồn dư — nói thẳng với khách hàng

1. **Malware có quyền user trên máy đang cắm token, PIN đã nhập** → dùng thẳng CryptoAPI, không cần agent.
   Agent **không phải** lớp phòng thủ ở đây; phòng thủ thật là rút token khi không dùng + endpoint security.
2. Người dùng bấm Yes không đọc → consent chỉ hiệu quả khi ít và rõ.
3. Server bị chiếm + user không nhìn preview → vẫn ký nhầm.
4. Agent bị thay thế trên máy đã bị chiếm → ghim public key không cứu được.

## 10. Còn phải chốt

Ba câu hỏi đầu đã có lời giải khi thi công:

1. ~~Topology **A hay B**~~ → **A**, xem §2. B để tối ưu sau, khi nghiệp vụ cần đóng tab mà lô vẫn chạy.
2. ~~PIN một lần/lô hay một lần/file~~ → **một lần/lô**, giữ key handle suốt phiên (`ISigningSession`).
3. ~~Agent nhận cả PDF hay chỉ `SignedAttributes`~~ → **chỉ `SignedAttributes`**. Nhẹ hơn hẳn, nhưng đổi lại
   **bỏ WYSIWYS**: plugin không nhìn thấy nội dung nên không tự kiểm được digest. Lấy lại được nếu sau này gửi
   kèm PDF đã prepare.

Còn treo:

4. Có nhắm **GPO/SCCM** không → cần MSI ngay từ đầu (NSIS không sinh MSI; cân nhắc WiX, nhớ kiểm license).
5. **Có cần macOS/Linux không** — nếu có thì phải đi PKCS#11, và khi đó "middleware tự hiện PIN dialog"
   **không còn đúng**, buộc phải tự nhận PIN → mâu thuẫn trực tiếp với nguyên tắc §4.
6. **Code signing (tối thiểu OV)** trước khi rollout thật — xem §8.
