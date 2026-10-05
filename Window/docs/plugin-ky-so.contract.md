# Contract — Plugin ký số ở máy người dùng

Plugin chạy trên máy người dùng, nghe **`http://127.0.0.1:17739`**. FE gọi thẳng, không qua BE. Không có
Bearer token — plugin không biết gì về tài khoản. Mã nguồn: [`../ks.plugin/`](../ks.plugin/README.md).

> Topology **A** (plugin mở listener loopback) là cái **đang chạy**; topology B (plugin tự gọi ra qua WSS) để
> **tối ưu sau**, xem [docs/bao-mat-agent-ky-so.md](bao-mat-agent-ky-so.md) §2. Trang `https://` gọi
> `http://127.0.0.1` **không** dính mixed content, nhưng Private Network Access của Chrome đã đổi cơ chế vài
> lần — phải test lại trên đúng bản trình duyệt đang dùng. `Origin` **không** phải hàng rào bảo mật.

## Routes

| Method | Route | Tham số | `data` trả về |
|---|---|---|---|
| GET | `api/plugin/trang-thai` | — | `TrangThai` |
| GET | `api/plugin/chung-thu-so` | `onlySignable?` (bool) | `CertScanResult` |
| POST | `api/plugin/chung-thu-so/kiem-tra-token` | body `{ thumbprint }` | `TokenVerify` |

Envelope giống hệt BE: `{ "status": 1, "data": …, "code": 200, "message": "Ok" }`, enum serialize thành **SỐ**.

## Ký hộ máy chủ — `api/plugin/ky-so`

| Method | Route | Body | `data` trả về |
|---|---|---|---|
| POST | `ky-so/mo-phien` | `{ thumbprint }` | `{ thumbprint, commonName, chungThuBase64 }` |
| POST | `ky-so/ky` | `{ yeuCau: [{ yeuCauId, duLieuBase64 }] }` | `[{ yeuCauId, chuKyBase64, loi }]` |
| POST | `ky-so/dong-phien` | — | `true` |

`mo-phien` là **chỗ duy nhất hộp PIN bật lên** trong luồng ký: mở khoá rồi GIỮ handle cho cả lô — khác cache
PIN, vì PIN vẫn đi thẳng từ bàn phím vào middleware. Phiên tự đóng sau **15 phút không dùng**
(`KySoConstants.IdleTimeoutMinutes`) hoặc khi **rút token** (quét cert store mỗi `SessionCheckSeconds = 2` giây,
chứng thư biến mất là đóng); sau đó `ky` trả `loi` nêu đúng lý do. `chungThuBase64` là chứng thư phần
**CÔNG KHAI** (DER), nộp lên máy chủ để dựng chuỗi tin cậy và lắp vào CMS.

⚠️ Chưa kiểm trên máy thật: giám sát rút token dựa vào việc middleware **gỡ** chứng thư khỏi store khi rút. Không
gỡ thì phiên chỉ đóng theo mốc 15 phút — đừng "sửa" bằng ký thử định kỳ: ký thử chạm khoá, có thể bật hộp PIN.

`ky` nhận **cả một đợt** yêu cầu: token ký tuần tự nên mỗi vòng đi-về cộng thẳng vào từng file, gom đợt là
cách duy nhất chia nhỏ khoản đó. Một yêu cầu hỏng trả `loi` riêng cho nó, các yêu cầu còn lại vẫn ký.

`duLieuBase64` là **SignedAttributes** do máy chủ dựng — plugin không nhận file nào vẫn ký được, đổi lại nó
**không tự kiểm được mình đang ký gì** (WYSIWYS ở
[docs/bao-mat-agent-ky-so.md](bao-mat-agent-ky-so.md) §5 chưa thi công) — nói thẳng khi bàn giao.

Route `ky-so/do-toc-do` đã **bỏ** (2026-10-05) ở cả hai bản: nó mở phiên và ký tới 100 lượt thật trên token —
một bề mặt chạm token không phục vụ người ký, lại đóng ngang phiên đang chạy nếu gọi giữa lô. Đo `T` bằng thời
gian một đợt `ky` chia cho số yêu cầu trong đợt.

## Dò plugin đã cài hay chưa

`GET api/plugin/trang-thai` là **phép dò**: gọi được nghĩa là máy đã cài và plugin đang chạy; timeout ngắn
(1–2 s) rồi coi như chưa cài, FE hiện popup tải bộ cài. Endpoint này **không** chạm chứng thư hay token nên
không bao giờ bật hộp thoại lên màn hình người dùng.

```jsonc
// TrangThai
{ "ten": "Plugin ký số", "phienBan": "3.0.0", "sanSang": true }
```

## TokenVerify — kiểm token cắm thật hay chưa

> Hai route bật hộp PIN, và chỉ hai: `chung-thu-so/kiem-tra-token`, `ky-so/mo-phien`. Đều vì
> cùng một lý do — chúng **chạm vào khoá bí mật**. Mọi phép đọc metadata thì không.

```jsonc
{
  "thumbprint": "A1B2…", "commonName": "…",
  "foundInStore": true, "hasPrivateKey": true, "notExpired": true,
  "allowsSigning": true, "onUsbToken": true, "canSignTest": true,
  "valid": true, "reason": null
}
```

`kiem-tra-token` **ký thử một mẩu dữ liệu ngẫu nhiên** bằng khoá bí mật của chứng thư. Đó là bằng chứng duy
nhất rằng token đang cắm thật và PIN dùng được — mọi phép đọc metadata đều có thể "đạt hết" trong khi token
đã rút từ lâu. Ký thử buộc phải chạm vào khoá, nên **middleware bit4id tự bật hộp nhập PIN của Windows**.

- **Liệt kê chứng thư KHÔNG bao giờ hỏi PIN.** Muốn hiện hộp PIN thì phải gọi endpoint này.
- **PIN không đi qua tiến trình plugin** — đi thẳng từ bàn phím vào middleware qua CNG/minidriver. Plugin
  không tự vẽ ô nhập PIN, xem [docs/bao-mat-agent-ky-so.md](bao-mat-agent-ky-so.md) §4.
- Kết quả chỉ là **một cờ `valid`** kèm `reason` hiển thị được: không trả số lần thử PIN còn lại.
- Plugin **không giữ handle khoá** sau khi kiểm: mỗi lần kiểm là một lần hỏi PIN. Luồng ký cả lô đi đường
  khác — `ky-so/mo-phien` giữ handle cho cả phiên nên N file chỉ hỏi PIN một lần; giữ handle **không** phải
  cache PIN.
- ⚠️ Màn ký số **bắt xác thực chứng thư trước khi mở nút Bắt đầu**, nên người dùng nhập PIN **hai lần** cho
  một lô (`kiem-tra-token` rồi `ky-so/mo-phien`). Coi `mo-phien` là phép xác thực thì bỏ được, đổi lại lỗi
  cert sai hiện muộn hơn — sau khi đã tải file lên. Chưa quyết.
- FE **không đặt timeout** cho lời gọi này: người dùng cần thời gian nhập PIN.

> ⚠️ Còn phải kiểm trên máy thật: hộp PIN có thể hiện chìm sau trình duyệt. SIPPACK set thuộc tính CNG
> `"HWND Handle"` trỏ vào cửa sổ app trước khi ký; plugin nay đã có `StatusWindow` (ẩn nhưng luôn có handle),
> chưa truyền handle đó vào CNG.

## Bộ cài và phiên bản — phát từ BE, không phải từ plugin

Cùng một plugin phục vụ **nhiều backend**: `ksts.be` (đòi Bearer token) và `kssm.be` (không auth — là API
external). Mỗi bên tự phát bộ cài của mình bằng cùng bộ route dưới đây; `dong-goi.ps1` chép một exe sang
`Plugins/` của cả hai.

| Method | Route | `data` trả về |
|---|---|---|
| GET | `api/core/plugin/bo-cai` | `{ "fileName": "Ký số plugin.exe", "exists": true }` |
| GET | `api/core/plugin/bo-cai/noi-dung` | **bytes exe thô, không envelope** |
| GET | `api/core/plugin/phien-ban?phienBan=1.0.0` | `{ phienBan, phuHop, danhSachPhuHop, lyDo }` |

Bộ cài là **một file exe tự cài**: bấm đúp là cài luôn middleware **bit4id** và **TokenManager** (token trước
2022) nhúng sẵn bên trong, cái nào máy còn thiếu thì cài, chép plugin
vào `%LocalAppData%\KySoPlugin` rồi chạy nền — không giải nén, không có file phụ để chạy nhầm. File (~162 MB)
nằm cạnh bản build BE tại `Plugins/Ký số plugin.exe`.

`exists = false` **không** phải lỗi — FE khoá nút tải; chỉ `bo-cai/noi-dung` thiếu file mới ném `1080 PluginSetupMissing`.

FE tải qua HttpClient rồi tạo blob, **không** `window.open`: route của `ksts.be` đòi Bearer token, mở trần nhận 401.

**Phiên bản**: FE lấy `phienBan` từ `trang-thai` rồi hỏi `phien-ban` của backend mình dùng. Whitelist ở
`appsettings.json` → `Plugin:PhienBanPhuHop`, khớp **chính xác cả chuỗi** chứ không so lớn-bé; sửa file là ăn
ngay, không khởi động lại API. Bỏ trống thì rơi về `PluginConstants.PhienBanPhuHopMacDinh` ghim trong mã — cố
ý, để bản triển khai thiếu cấu hình không đánh trượt mọi plugin. `phuHop = false` **không** ném lỗi: người
dùng không gây ra chuyện đó và vẫn cần đọc `lyDo`.

`PluginConstants.PhienBan` **đọc lại từ assembly** (2026-09-02) nên `<Version>` của `ks.plugin.api.csproj`
là nguồn duy nhất; nâng bản thì chỉ còn phải thêm giá trị mới vào whitelist của **mọi** backend đang dùng
plugin — thiếu một backend là bên đó báo plugin lỗi thời ngay sau khi người dùng cập nhật. Đánh số theo SemVer:
phá hợp đồng ⇒ MAJOR; `3.0.0` (2026-10-05) vì bỏ `ky-so/do-toc-do`.

⚠️ .NET gắn thêm commit SHA vào `InformationalVersion` (`1.0.0+0e8060f9…`) nên hằng số phải cắt ở dấu `+`; bỏ
bước cắt là chuỗi gửi lên không bao giờ khớp whitelist và **mọi** plugin bị đánh là lỗi thời.

⚠️ Exe nay tên **`Ký số plugin.exe`** (2026-09-02) — có dấu và có khoảng trắng nên mọi chỗ dựng lệnh phải bọc
nháy; thư mục cài và khoá registry vẫn là `KySoPlugin`. Máy đã cài bản `KstsPlugin` cũ vẫn **cài song song**
chứ không đè lên: phải gỡ bản cũ bằng tay một lần, đừng "sửa" bằng cách đưa tên cũ trở lại.

## CertScanResult

```jsonc
{
  "certificates": [
    {
      "subject": "CN=Trường Đại học Xây dựng Hà Nội, O=…", "commonName": "Trường Đại học Xây dựng Hà Nội",
      "issuer": "CN=CA phục vụ các cơ quan Nhà nước G2, …", "issuerCommonName": "CA phục vụ các cơ quan Nhà nước G2",
      "serialNumber": "540E…", "thumbprint": "A1B2C3…", "source": 2, "keyProvider": "bit4id xPKI CSP",
      "validFrom": "01/01/2026 00:00:00", "validTo": "01/01/2029 00:00:00",
      "hasPrivateKey": true, "isExpired": false, "allowsSigning": true, "reason": null
    }
  ],
  "storeDiagnostics": ["CurrentUser\\My: 12 chứng thư", "LocalMachine\\My: 2 chứng thư"]
}
```

- `source`: `0` Local · `1` Server · `2` UsbToken.
- `thumbprint` là **khoá định danh** — dùng nó khi chọn cert và khi lưu vào template.
- `reason` = lý do **tại máy** khiến cert không ký được, xét theo thứ tự: thiếu khoá bí mật → hết hạn →
  KeyUsage không cho ký → **khoá không nằm trên USB token**; `null` là qua hết, FE hiện "Ký được". Cert trong
  kho phần mềm (`source` = `0`/`1`) luôn **"Không ký được"** — khoá phần mềm sao chép được nên không đủ tư
  cách ký giấy báo trúng tuyển. Hiển thị `reason` nguyên văn cho người dùng.
- **Không có `canSign`, không có `isTrusted`.** Plugin chạy trên máy không kiểm soát được nên cờ tin cậy nó
  gửi lên là vô giá trị — thẩm định chuỗi về CA đã ghim là việc của BE (`ICertificateTrustValidator`), xem
  [docs/bao-mat-agent-ky-so.md](bao-mat-agent-ky-so.md) §7.
- `storeDiagnostics` để chẩn đoán khi danh sách rỗng. Đặt sau nút "Chẩn đoán", không hiện mặc định.
- **Danh sách không được cache** — gọi lại mỗi lần mở màn chọn cert: token có thể vừa cắm hoặc vừa rút.
- Bước lấy danh sách **không bật hộp thoại PIN**: plugin chỉ đọc metadata của khoá, không ký thử.

## Vì sao không cần DLL nào trong plugin

Plugin đọc chứng thư qua **Windows certificate store** (`X509Store`), kể cả cert trên USB token — middleware
(bit4id, hoặc TokenManager cho token đời cũ) tự bắc cầu khoá vào store qua minidriver/CSP, không cần nạp `.dll` PKCS#11 nào. Đổi lại **máy phải cài
middleware** thì token mới hiện trong store — đó là lý do bộ cài gói cả hai vào một exe.

## CORS

Origin của FE khai ở `ks.plugin.api/appsettings.json` → `Cors:AllowedOrigins`, và ghim thêm trong
`PluginConstants.OriginMacDinh` vì bản phát hành là một exe không kèm file cấu hình. Thiếu CORS thì trình
duyệt không đọc được kết quả; đây là điều kiện để chạy, **không** phải lớp bảo mật.

Danh sách đang khai: `https://ksts.yna.io.vn`, `localhost:4200` và `localhost:3000` (mỗi cổng cả `http` lẫn
`https`). Cổng 3000 thêm ngày 2026-09-02 cho phía gọi `kssm.be` chạy dưới máy phát triển.

⚠️ **Chưa khai origin prod của FE gọi `kssm.be`.** Quên bước này lúc rollout thì triệu chứng là "đã cài plugin
mà trang web vẫn báo chưa cài", rất tốn công dò: request vẫn tới plugin và vẫn vào log, nhưng trình duyệt vứt
bỏ câu trả lời vì thiếu tiêu đề `Access-Control-Allow-Origin`. Thêm origin xong phải **đóng gói lại** — danh
sách ghim trong mã, sửa `appsettings.json` của bản phát hành không ăn.

⚠️ **Bản macOS giữ một bản sao danh sách này** ở `MacOS/ks-plugin/crates/ks-plugin-shared/src/constants.rs`
(`DEFAULT_ORIGINS`). Thêm hay bớt origin thì sửa **cả hai** chỗ rồi đóng gói lại cả hai bản — sót một bên là bên đó
báo "chưa cài plugin".
