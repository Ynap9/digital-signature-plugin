# Plugin ký số cho Windows

Bản Windows của plugin ký số, viết bằng .NET 9. Ứng dụng chạy trên **máy người dùng**, làm cầu nối giữa trang
web ký số và USB token. Đọc [README tổng](../../README.md) để nắm bối cảnh trước.

Hợp đồng API, luồng ký và thiết kế bảo mật: [../docs/](../docs/README.md).

.NET 9 cho Windows, phát hành thành **một file `.exe` là trình cài đặt NSIS có wizard**, mang sẵn bản plugin
self-contained bên trong: máy người dùng không cần .NET runtime, không phải giải nén, không có file phụ nào
để chạy nhầm.

## Mục lục

- [Vì sao cần plugin](#vì-sao-cần-plugin)
- [Kiến trúc](#kiến-trúc)
- [API](#api)
- [Quan hệ với middleware của token](#quan-hệ-với-middleware-của-token)
- [Đóng gói bộ cài](#đóng-gói-bộ-cài)
- [Luồng cài đặt](#luồng-cài-đặt)
- [Chạy khi phát triển](#chạy-khi-phát-triển)
- [Nguyên tắc bảo mật](#nguyên-tắc-bảo-mật)
- [Việc còn lại](#việc-còn-lại)

## Vì sao cần plugin

Khoá bí mật của chứng thư số nằm trong chip USB token và không trích xuất ra được. Ký một tài liệu nghĩa là
ra lệnh cho chip ký, thao tác này phải gọi API native của Windows (CNG/CSP) nên **JavaScript trong trình
duyệt không làm được**.

Đó là lý do mọi giải pháp ký số USB token tại Việt Nam đều yêu cầu cài một ứng dụng nhỏ trên máy. Plugin này
là ứng dụng đó.

## Kiến trúc

Bốn project, cùng cách chia tầng với backend:

```
ks.plugin.api            Controller, Program.cs (dựng web host + cấu hình CORS)
ks.plugin.applications   Nghiệp vụ mỏng: đọc chứng thư, ký số
ks.plugin.external       Đọc chứng thư, kiểm tra token, khay hệ thống + cửa sổ trạng thái, nhật ký, đọc cấu hình
ks.plugin.shared         Hằng số, envelope ApiResponse
```

`Ký số plugin.exe` chỉ còn **một vai — plugin**. Việc cài đặt do trình cài đặt NSIS (`bo-cai.nsi`) lo trọn:
chép file, bật tự khởi động, ghi mục gỡ cài đặt, cài middleware. Plugin không tự cài chính nó nữa.

Exe build dạng **`WinExe`**, không có console: trên Windows 11 console mở trong Windows Terminal và `ShowWindow`
không ẩn được tab đó, nên bản console cũ bật cửa sổ đen mỗi lần khởi động máy. Thay vào đó là `StatusWindow`
(`external/Tray`) — form WinForms nhẹ hiện phiên bản, địa chỉ nghe và nhật ký. Nhật ký đi qua `LogBufferProvider`
vào `LogBuffer` (`external/Logging`): chỉ giữ 1000 dòng cuối **trong RAM**, không ghi xuống đĩa. Bản phát hành
không kèm `appsettings.json` nên mức log đặt bằng code trong `Program.cs`: `Microsoft.AspNetCore` và
`Microsoft.Hosting.Lifetime` chỉ từ `Warning` (dòng *Press Ctrl+C* của Lifetime vô nghĩa khi không có console).

Phiên bản, nhà phát hành `YnaP` và copyright cùng khai ở `ks.plugin.api.csproj` (`<Version>`, `<Company>`,
`<Copyright>`); cửa sổ đọc lại copyright từ assembly. `<FileVersion>` = `$(Version).0` cho đúng dạng bốn phần
của Windows. Icon `Assets/ky-so.ico` (bút + nét ký, phong cách Fluent, 16–256 px) nhúng làm cả icon exe lẫn
icon khay.

⚠️ Không còn console nên lỗi lúc khởi động (cổng 17739 bị chiếm…) phải hiện bằng `MessageBox`; đừng bỏ khối
`try` quanh `app.Start()` — thiếu nó là plugin chết im lặng.

Trình cài đặt ghi lựa chọn của người dùng vào `HKCU\Software\KySoPlugin`; lúc khởi động plugin đọc lại giá
trị `MoiTruong` ở đó làm `ASPNETCORE_ENVIRONMENT` (`ICauHinhCaiDat` ở `external/CauHinh`). Không có giá trị
thì ASP.NET Core dùng mặc định của nó.

Plugin nghe tại `http://127.0.0.1:17739`, **chỉ trên loopback**, không lộ ra mạng LAN. Envelope trả về giống
hệt backend để frontend dùng chung một cách đọc:

```jsonc
{ "status": 1, "data": {}, "code": 200, "message": "Ok" }
```

Origin của trang web được phép đọc kết quả khai ở **`PluginConstants.OriginMacDinh`**, cộng thêm phần khai
trong `appsettings.json` nếu có. Ghim trong mã vì bản phát hành là một file exe **không kèm file cấu hình**;
`appsettings.json` giờ chỉ còn để bổ sung origin lúc phát triển. Đây là **điều kiện để trình duyệt đọc được
kết quả, không phải lớp bảo mật**: header `Origin` do phía gọi tự đặt, `curl` hay mã độc đặt tuỳ ý.

⚠️ **Đưa trang web lên tên miền mới thì phải thêm origin đó rồi đóng gói lại.** Thiếu bước này, triệu chứng
trông y hệt *chưa cài plugin*: request vẫn tới plugin và vẫn được ghi log, nhưng trình duyệt vứt bỏ câu trả
lời nên phép dò `trang-thai` rơi vào nhánh lỗi. Nhìn log plugin thấy có request mà giao diện vẫn báo chưa cài
thì kiểm CORS trước tiên.

## API

| Method | Route | Việc |
|---|---|---|
| GET | `api/plugin/trang-thai` | Phép dò: gọi được nghĩa là máy đã cài plugin và plugin đang chạy; trả kèm phiên bản |
| GET | `api/plugin/chung-thu-so` | Liệt kê chứng thư trong kho của Windows |
| POST | `api/plugin/chung-thu-so/kiem-tra-token` | Ký thử một mẩu dữ liệu để xác nhận token dùng được |
| POST | `api/plugin/ky-so/mo-phien` | Mở khoá trên token và GIỮ handle cho cả lô; trả chứng thư phần công khai |
| POST | `api/plugin/ky-so/ky` | Ký cả một đợt yêu cầu bằng handle đã mở, không hỏi PIN lại |
| POST | `api/plugin/ky-so/dong-phien` | Đóng phiên, giải phóng handle khoá |

Ba điểm quan trọng về hành vi:

- **Liệt kê chứng thư không bao giờ hỏi mã PIN.** Nó chỉ đọc metadata của khoá.
- **Hai route chạm vào khoá bí mật thì bật hộp PIN**: `kiem-tra-token` và `ky-so/mo-phien`.
  Đó cũng là bằng chứng duy nhất rằng token đang cắm thật — mọi phép đọc metadata đều có thể "đạt hết" trong
  khi token đã rút từ lâu.
- **Phiên bản trả ở `trang-thai` đọc từ assembly**, tức `<Version>` trong `ks.plugin.api.csproj` là nguồn
  duy nhất. Backend đối chiếu chuỗi đó với whitelist của mình qua `GET api/core/plugin/phien-ban`.

Kết quả liệt kê không có cờ `isTrusted`. Máy người dùng không kiểm soát được nên cờ tin cậy do nó gửi lên là
vô giá trị; thẩm định chuỗi chứng thư về CA gốc là việc của backend.

Chứng thư nằm trong kho phần mềm của máy luôn bị đánh dấu **không ký được**: ký giấy báo trúng tuyển phải
bằng khoá trên token, khoá phần mềm sao chép được nên không đủ tư cách.

## Quan hệ với middleware của token

Plugin đọc chứng thư qua **Windows certificate store** (`X509Store`), kể cả chứng thư nằm trên USB token.
Plugin **không nạp thư viện PKCS#11 nào** và không gọi trực tiếp vào phần mềm của hãng token.

Cầu nối là **middleware của hãng token**: nó đăng ký một provider mật mã với Windows, nhờ đó chứng thư trên
token hiện ra trong certificate store như chứng thư thường. Token của Ban Cơ yếu có hai đời, mỗi đời một
middleware:

| Token | Middleware | Provider đăng ký |
|---|---|---|
| Đời mới | bit4id Universal MW | `Bit4id Universal Middleware Provider`, `Bit4id Key Storage Provider` |
| Đời cũ, trước 2022 | TokenManager (cài VGCA Client 8.3 của SafeNet) | CSP `eToken Base Cryptographic Provider`, KSP `SafeNet Smart Card Key Storage Provider` |

Token đời cũ đăng ký khoá qua **CSP của CAPI** chứ không phải KSP của CNG, nên .NET trả `RSACryptoServiceProvider`
thay vì `RSACng`. `KeyProviders` (`external/Certificates`) đọc tên provider của cả hai kiểu; dò riêng `RSACng` là
token cũ bị đánh là *khoá nằm trong kho phần mềm* dù đang cắm thật.

Hệ quả: **máy chưa cài middleware thì plugin không thấy token**. Bộ cài vì thế kèm cả hai middleware.

## Đóng gói bộ cài

```powershell
cd ks.plugin
./dong-goi.ps1
```

Script publish plugin self-contained, gọi `makensis` đóng `bo-cai.nsi` thành bộ cài, rồi chép kết quả vào
`bo-cai/` của repo này và vào `Plugins/` của backend nào **thực sự có trong workspace** (không có thì bỏ qua,
không tạo thư mục rỗng).

Kết quả là **một file** `Ký số plugin.exe` (~162 MB): trình cài đặt NSIS mang sẵn plugin self-contained (máy
người dùng không cần .NET runtime) và **hai bộ cài middleware** lấy từ `vendor/bit4id/` và
`vendor/vgca-tokenmanager/`. Tên file giữ nguyên như
bản cũ nên backend không phải sửa `SetupFileName`.

Cần **NSIS 3** trên máy đóng gói; `dong-goi.ps1` tự tìm `makensis.exe` ở `Program Files` hoặc trên PATH và
dừng với thông báo rõ nếu thiếu.

Một file thay vì file nén là quyết định có lý do: bước dễ hỏng nhất của bản cũ là người dùng giải nén rồi
chạy nhầm `Ký số plugin.exe` thay vì `CAI-DAT.cmd` — plugin lên nhưng middleware không được cài, mà triệu
chứng thì giống hệt "chưa cài gì cả". Không còn file nào để chạy nhầm thì không còn lỗi đó.

Backend phát file này qua `api/core/plugin/bo-cai/noi-dung`. Sau khi đóng gói phải **build lại `ksts.be.api`**
để file được chép sang thư mục output.

### Đưa bộ cài lên máy chủ

File exe **không nằm trong git** (~162 MB, là sản phẩm build), nên máy chủ dựng image từ bản clone của repo sẽ
không có nó — thiếu bước này thì màn Ký số báo *"Máy chủ chưa có bộ cài plugin"*. Chép tay lên thư mục đã
mount sẵn vào container:

```bash
scp "ksts.be/ksts.be.api/Plugins/Ký số plugin.exe" <user>@<may-chu>:<repo>/ksts.be/ksts.be.api/Plugins/
scp "kssm.be/kssm.be.api/Plugins/Ký số plugin.exe" <user>@<may-chu>:<repo>/kssm.be/kssm.be.api/Plugins/
```

`deploy/docker-compose.yml` mount thẳng thư mục đó vào `/app/Plugins` chỉ đọc, nên bản mới có hiệu lực ngay,
không phải build lại image cũng không phải khởi động lại container. Chi tiết:
[`ksts.be/ksts.be.api/Plugins/README.md`](../ksts.be/ksts.be.api/Plugins/README.md).

### Middleware không nằm trong repo

`vendor/bit4id/` và `vendor/vgca-tokenmanager/` là chỗ cắm sẵn cho file cài middleware, nhưng file đó là
**phần mềm của hãng token**, phải lấy từ đơn vị cấp chứng thư số. Thiếu file nào thì vẫn đóng gói được, chỉ là
bản ra không tự cài middleware đó và người dùng phải tự cài trước. Cách lấy và mã băm từng bản: file `.md`
trong mỗi thư mục.

`dong-goi.ps1` tìm file `.exe`/`.msi` đầu tiên trong mỗi thư mục rồi truyền đường dẫn vào `makensis` qua define
`BIT4ID_SETUP` và `TOKEN_MANAGER_SETUP`, kèm đuôi file qua `*_SETUP_EXT`. Lúc cài, NSIS bung file ra
`$PLUGINSDIR` (thư mục tạm tự dọn) **giữ nguyên đuôi** rồi chạy. Define nào không được khai thì bộ cài chỉ in
một dòng nhắc người dùng tự cài middleware đó.

Cách chạy ngầm chọn theo đuôi file (macro `RunVendorSetup` trong `bo-cai.nsi`):

| Đuôi | Lệnh |
|---|---|
| `.msi` | `msiexec.exe /i "<file>" /qn /norestart` (`MSI_SILENT_FLAGS`) |
| `.exe` | `<file> /S` (`SILENT_FLAG`) — cả bit4id lẫn TokenManager đang dùng đều đóng bằng NSIS |

Bộ cài đóng bằng InstallShield hay Inno Setup thì ghi cờ đúng vào `vendor/<thư mục>/tham-so.txt` (dòng đầu không
bắt đầu bằng `#`) — sai cờ là trình cài đứng im chờ một hộp thoại không ai nhìn thấy. `dong-goi.ps1` chuyển cờ
sang NSIS qua một file include sinh tạm (`vendor-args.nsh` → define `*_SETUP_ARGS`), **không** qua dòng lệnh:
PowerShell 5.1 làm rơi dấu nháy kép khi gọi chương trình ngoài, mà cờ InstallShield có dạng `/s /v"/qn"`.

⚠️ Bản trước bung mọi file thành `*-setup.exe` rồi chạy kèm `/S`, nên một bộ cài `.msi` không bao giờ chạy được
và người dùng chỉ thấy dòng *CHƯA cài được*. Đừng đổi lại thành tên cố định `.exe`.

Không tự tải middleware từ Internet về. Đây là phần mềm đụng tới kho khoá mật mã của cả máy và được cài ngầm
với quyền quản trị; tải một binary không rõ nguồn rồi làm vậy đúng là kịch bản một cuộc tấn công chuỗi cung
ứng cần.

## Luồng cài đặt

Toàn bộ việc cài nằm trong `bo-cai.nsi`. Người dùng bấm đúp một lần rồi đi qua wizard như mọi ứng dụng khác:

```
1. Chào mừng                 -> Tiếp
2. Chọn thư mục cài          -> mặc định %LocalAppData%\KySoPlugin
3. Chọn môi trường           -> Production / Staging / Development
4. Tiến độ cài
   ├─ dừng bản plugin đang chạy (trừ chính trình cài đặt)
   ├─ middleware token, lần lượt bit4id rồi TokenManager, mỗi cái xét riêng
   │  ├─ máy đã có provider của nó  -> bỏ qua
   │  ├─ chưa có, bộ cài kèm sẵn    -> xin quyền quản trị, chạy ngầm, kiểm lại provider
   │  └─ chưa có, không kèm         -> báo rõ rồi vẫn cài plugin (sẽ không thấy token đời đó)
   ├─ chép plugin vào thư mục đã chọn
   ├─ ghi HKCU\Software\KySoPlugin (ThuMucCai, MoiTruong)
   ├─ bật tự khởi động HKCU\...\Run
   └─ ghi mục gỡ cài đặt vào Apps & Features
5. Hoàn tất                  -> Finish, tuỳ chọn chạy plugin ngay
```

Gỡ bằng Apps & Features, hoặc chạy `go-cai-dat.exe` trong thư mục cài. Lượt gỡ xoá tự khởi động, mục gỡ cài
đặt, khoá cấu hình và file plugin.

Cách nhận biết middleware đã có: hỏi thẳng danh sách provider mật mã đã đăng ký với Windows trong registry
(`SetRegView 64` để đọc đúng nhánh 64-bit): provider bắt đầu bằng `bit4id`/`bit4xpki` là có bit4id, bắt đầu bằng
`eToken` là có TokenManager; không thấy thì dò thêm `System32\bit4*.dll` và `System32\eTOKCSP.dll`. Không dò
tên trong Programs and Features. Thứ quyết định token có
hiện trong certificate store là **provider có được đăng ký hay không**; mục trong Programs and Features chỉ
nói ai đó từng chạy bộ cài, có bản gỡ lỗi để lại mục mà mất provider. Sau khi chạy bộ cài middleware, trình
cài đặt **kiểm lại provider** thay vì tin vào mã thoát.

Bộ cài khai `RequestExecutionLevel user` nên cài **per-user** vào `%LocalAppData%\KySoPlugin`, tự khởi động
qua `HKCU\...\Run`, không cài driver, không dựng service SYSTEM. Nhờ vậy phần cài plugin **không cần quyền
quản trị**; chỉ bước cài middleware mới nâng quyền qua `ExecShellWait "runas"`, và chỉ khi thực sự phải cài.

⚠️ Chọn thư mục cài nằm trong `Program Files` thì bước chép file sẽ thất bại vì bộ cài không chạy quyền quản
trị. Mặc định để trong hồ sơ người dùng là có lý do.

Gỡ plugin **không** gỡ middleware nào: đó là phần mềm dùng chung cho mọi ứng dụng chữ ký số trên máy.

⚠️ Chưa chạy thử TokenManager với `/S` trên máy sạch: vỏ NSIS tắt được wizard của nó, nhưng chưa xác nhận bộ
cài SafeNet bên trong cũng chạy ngầm. Bên trong còn bật cửa sổ thì bộ cài plugin đứng chờ — thử trước khi phát
hành, xem [`vendor/vgca-tokenmanager/`](vendor/vgca-tokenmanager/DAT-BO-CAI-TOKENMANAGER-VAO-DAY.md).

## Chạy khi phát triển

```bash
cd ks.plugin/ks.plugin.api
dotnet run
```

Kiểm tra nhanh:

```bash
curl http://127.0.0.1:17739/api/plugin/trang-thai
curl "http://127.0.0.1:17739/api/plugin/chung-thu-so?onlySignable=false"
```

Máy phát triển phải cài middleware của token thì mới liệt kê được chứng thư thật.

`dotnet run` **không cài gì** lên máy đang phát triển: exe chỉ còn vai plugin, việc cài nằm hẳn ở bộ cài NSIS.
Muốn thử luồng cài thì chạy `./dong-goi.ps1` rồi chạy file exe sinh ra trong `bo-cai/`.

Lúc phát triển chưa có khoá `HKCU\Software\KySoPlugin` nên plugin dùng môi trường mặc định của ASP.NET Core.
Muốn thử một môi trường cụ thể mà không cần chạy bộ cài thì ghi tay giá trị đó:

```powershell
New-Item -Path "HKCU:\Software\KySoPlugin" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\KySoPlugin" -Name "MoiTruong" -Value "Development"
```

## Nguyên tắc bảo mật

**Không bao giờ chạm vào mã PIN.** Để middleware tự bật hộp nhập PIN qua CNG; PIN đi thẳng từ bàn phím vào
provider mật mã. Tiến trình plugin không thấy một byte PIN nào. Tuyệt đối không tự vẽ ô nhập PIN — tự viết là
giữ PIN trong bộ nhớ, có thể bị dump, bị keylog, hoặc lỡ tay ghi vào log.

**Không cache danh sách chứng thư xuống đĩa.** Liệt kê lại mỗi lần: token có thể vừa được cắm hoặc vừa rút.

**Phân biệt giữ handle khoá và cache PIN.** Giữ handle một phiên là thứ khiến cả lô chỉ hỏi PIN một lần; đó
không phải cache PIN. Middleware có thể tự cache PIN theo cấu hình riêng của nó, nên code sạch là chưa đủ,
phải kiểm cả cấu hình middleware.

**Ghi log không kèm PIN và không kèm nội dung file.** Chỉ ghi thời điểm, mã công việc, vân tay chứng thư và
số lượng file.

Rủi ro còn lại phải nói thẳng với khách hàng: mã độc có quyền người dùng trên máy đang cắm token và PIN đã
nhập thì gọi thẳng CryptoAPI được, không cần qua plugin. Plugin **không phải** lớp phòng thủ ở đây; phòng thủ
thật là rút token khi không dùng và bảo vệ máy trạm.

## Việc còn lại

**Chuyển sang topology B.** Hiện plugin mở cổng nghe ở loopback và frontend gọi vào. Thiết kế đã chốt là
plugin **tự gọi ra máy chủ qua WebSocket** để nhận việc, không mở cổng nào cả: như vậy không trang web nào
gọi được plugin, hết chuyện mixed content, hết Private Network Access, hết xung đột cổng. Kế hoạch chi tiết
nằm ở `.claude/plugin/plans/ky-so-plugin.plan.md`.

**Xác minh job ticket** do máy chủ ký, với public key ghim cứng lúc build, và tự tính lại giá trị băm từ
bytes PDF thật trước khi ký.

**Ký mã nguồn (code signing)** trước khi phát hành rộng. Binary chưa ký thì SmartScreen chặn ở lần chạy đầu;
máy cơ quan thường bật chính sách không cho bỏ qua cảnh báo, và phần mềm diệt virus xem binary chưa ký chạy
nền, tự khởi động, đụng crypto token, kết nối ra ngoài là chân dung mã độc điển hình.

**Hộp PIN hiện chìm.** Hộp PIN có thể nằm sau trình duyệt vì không gắn với cửa sổ nào. Plugin nay đã có cửa sổ
trạng thái (ẩn khi chạy nền, luôn có handle); việc còn lại là truyền handle đó vào thuộc tính CNG
`"HWND Handle"` trước khi ký.
