# Plugin ký số

Ứng dụng cục bộ cho phép trang web ký tài liệu bằng USB token, qua một API HTTP trên loopback.

Bất kỳ hệ thống ký số nào trên web đều dùng được: plugin không biết gì về nghiệp vụ của bên gọi, nó chỉ nhận
giá trị băm, ra lệnh cho token ký, rồi trả chữ ký về.

## Mục lục

- [Bối cảnh](#bối-cảnh)
- [Nền tảng hỗ trợ](#nền-tảng-hỗ-trợ)
- [Yêu cầu](#yêu-cầu)
- [Cài đặt](#cài-đặt)
- [Sử dụng](#sử-dụng)
- [API](#api)
- [Tích hợp vào một hệ thống ký số](#tích-hợp-vào-một-hệ-thống-ký-số)
- [Cấu hình](#cấu-hình)
- [Build từ mã nguồn](#build-từ-mã-nguồn)
- [Bảo mật](#bảo-mật)
- [Lộ trình](#lộ-trình)
- [Đóng góp](#đóng-góp)
- [Liên hệ](#liên-hệ)
- [Giấy phép](#giấy-phép)

## Bối cảnh

Khoá bí mật của chứng thư số nằm trong chip USB token và không trích xuất ra được. Ký một tài liệu nghĩa là ra
lệnh cho chip ký, thao tác này phải gọi API native của hệ điều hành nên JavaScript trong trình duyệt không làm
được.

Cách chia việc vì thế là: **ký ở máy người dùng, lắp ráp ở máy chủ**. Máy chủ dựng tài liệu và tính giá trị
băm; plugin dùng token ký giá trị băm đó; máy chủ nhận chữ ký về rồi ghi vào file. Cái đi qua mạng chỉ là giá
trị băm và phần công khai của chứng thư — mã PIN và khoá bí mật không bao giờ rời khỏi máy người dùng.

Đó là lý do mọi giải pháp ký số USB token tại Việt Nam đều yêu cầu cài một ứng dụng nhỏ trên máy.

## Nền tảng hỗ trợ

| Nền tảng | Thư mục | Ngôn ngữ | Trạng thái |
|---|---|---|---|
| Windows 10/11 x64 | [`Window/ks.plugin`](Window/ks.plugin/README.md) | .NET 9 | Khả dụng |
| macOS | [`MacOS`](MacOS/README.md) | Rust | Đang phát triển |

Hai bản là hai ứng dụng riêng, khác cả ngôn ngữ, không dùng chung mã nguồn: đường vào token khác nhau ở mức hệ
điều hành nên không che được bằng một lớp trừu tượng. Thứ giữ cho chúng thay thế được nhau là [API](#api),
không phải mã nguồn — bên gọi không cần biết máy người dùng chạy hệ điều hành nào.

## Yêu cầu

Để chạy plugin:

- Windows 10 hoặc 11, 64-bit.
- USB token và **middleware của hãng token** đã cài trên máy.

Middleware là bắt buộc, không phải tuỳ chọn. Nó đăng ký một provider mật mã với hệ điều hành, nhờ đó chứng thư
trên token hiện ra trong kho chứng thư của Windows như chứng thư thường. Plugin đọc kho đó chứ không nạp thư
viện PKCS#11 nào. Máy chưa cài middleware thì plugin chạy được nhưng không thấy token.

Bộ cài mang sẵn middleware và tự cài nếu máy chưa có, nên thông thường không phải làm gì thêm.

Để build từ mã nguồn, xem [Build từ mã nguồn](#build-từ-mã-nguồn).

## Cài đặt

Tải bộ cài rồi chạy. Wizard gồm năm bước:

```
1. Chào mừng
2. Chọn thư mục cài       mặc định %LocalAppData%\KySoPlugin
3. Chọn môi trường        Production / Staging / Development
4. Tiến độ cài            cài middleware nếu thiếu, chép plugin, ghi registry
5. Hoàn tất               tuỳ chọn chạy plugin ngay
```

Bộ cài chạy ở quyền người dùng thường: không cài driver, không dựng service hệ thống, tự khởi động qua
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Chỉ bước cài middleware mới xin nâng quyền, và chỉ khi
máy thực sự chưa có provider.

Không chọn thư mục cài trong `Program Files`: bộ cài không chạy quyền quản trị nên bước chép file sẽ thất bại.

Gỡ bằng Apps & Features, hoặc chạy `go-cai-dat.exe` trong thư mục cài. Lượt gỡ **không** gỡ middleware: đó là
phần mềm dùng chung cho mọi ứng dụng chữ ký số trên máy.

## Sử dụng

Sau khi cài, plugin chạy nền ở khay hệ thống và tự khởi động cùng Windows. Chỉ một bản chạy tại một thời điểm.

Kiểm tra plugin đang chạy:

```bash
curl http://127.0.0.1:17739/api/plugin/trang-thai
```

```jsonc
{ "status": 1, "data": { "phienBan": "2.0.0" }, "code": 200, "message": "Ok" }
```

Liệt kê chứng thư, kể cả chứng thư không ký được:

```bash
curl "http://127.0.0.1:17739/api/plugin/chung-thu-so?onlySignable=false"
```

Một lô ký đi theo trình tự: `mo-phien` để mở khoá trên token một lần, `ky` nhiều lần cho từng đợt yêu cầu, rồi
`dong-phien` để giải phóng handle khoá.

## API

Plugin nghe tại `http://127.0.0.1:17739`, **chỉ trên loopback**, không lộ ra mạng LAN.

Mọi phản hồi dùng chung một envelope:

```jsonc
{ "status": 1, "data": {}, "code": 200, "message": "Ok" }
```

| Method | Route | Việc |
|---|---|---|
| GET | `api/plugin/trang-thai` | Phép dò: gọi được nghĩa là máy đã cài plugin và plugin đang chạy; trả kèm phiên bản |
| GET | `api/plugin/chung-thu-so` | Liệt kê chứng thư trong kho của hệ điều hành |
| POST | `api/plugin/chung-thu-so/kiem-tra-token` | Ký thử một mẩu dữ liệu để xác nhận token dùng được |
| POST | `api/plugin/ky-so/mo-phien` | Mở khoá trên token và giữ handle cho cả lô; trả chứng thư phần công khai |
| POST | `api/plugin/ky-so/ky` | Ký cả một đợt yêu cầu bằng handle đã mở, không hỏi PIN lại |
| POST | `api/plugin/ky-so/do-toc-do` | Đo thời gian một lượt ký thật trên token |
| POST | `api/plugin/ky-so/dong-phien` | Đóng phiên, giải phóng handle khoá |

Ba điểm về hành vi cần biết trước khi tích hợp:

- **Liệt kê chứng thư không bao giờ hỏi mã PIN.** Nó chỉ đọc metadata của khoá.
- **Ba route chạm vào khoá bí mật thì bật hộp PIN**: `kiem-tra-token`, `ky-so/mo-phien` và `ky-so/do-toc-do`.
  Đó cũng là bằng chứng duy nhất rằng token đang cắm thật — mọi phép đọc metadata đều có thể "đạt hết" trong
  khi token đã rút từ lâu.
- **Kết quả liệt kê không có cờ tin cậy.** Máy người dùng không kiểm soát được nên cờ do nó gửi lên là vô giá
  trị; thẩm định chuỗi chứng thư về CA gốc là việc của phía máy chủ.

Chứng thư nằm trong kho phần mềm của máy luôn bị đánh dấu không ký được: khoá phần mềm sao chép được nên không
đủ tư cách cho chữ ký có giá trị pháp lý.

## Tích hợp vào một hệ thống ký số

Hai việc phía gọi phải làm.

**Khai origin của trang web.** Danh sách origin được phép đọc kết quả ghim trong mã nguồn tại
`PluginConstants.OriginMacDinh`, vì bản phát hành là một file thực thi không kèm file cấu hình. Thêm tên miền
mới thì phải sửa hằng số đó rồi đóng gói lại.

Đây là **điều kiện để trình duyệt đọc được kết quả, không phải lớp bảo mật**: header `Origin` do phía gọi tự
đặt, `curl` hay mã độc đặt tuỳ ý.

Thiếu bước này, triệu chứng trông y hệt *chưa cài plugin*: request vẫn tới plugin và vẫn được ghi log, nhưng
trình duyệt vứt bỏ câu trả lời nên phép dò `trang-thai` rơi vào nhánh lỗi. Nhìn log plugin thấy có request mà
giao diện vẫn báo chưa cài thì kiểm CORS trước tiên.

**Đối chiếu phiên bản.** `trang-thai` trả chuỗi phiên bản đọc từ assembly. Phía máy chủ tự quyết định chấp nhận
những phiên bản nào; nâng phiên bản plugin thì phải cập nhật danh sách chấp nhận ở **mọi** hệ thống đang dùng,
thiếu một bên là bên đó báo plugin lỗi thời ngay sau khi người dùng cập nhật.

## Cấu hình

Bản phát hành không kèm file cấu hình. Trình cài đặt ghi lựa chọn của người dùng vào registry, plugin đọc lại
lúc khởi động:

| Khoá | Giá trị | Ý nghĩa |
|---|---|---|
| `HKCU\Software\KySoPlugin` | `ThuMucCai` | Thư mục đã cài |
| `HKCU\Software\KySoPlugin` | `MoiTruong` | Dùng làm `ASPNETCORE_ENVIRONMENT` |

Không có khoá thì plugin dùng môi trường mặc định của ASP.NET Core. Đặt tay một môi trường khi phát triển:

```powershell
New-Item -Path "HKCU:\Software\KySoPlugin" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\KySoPlugin" -Name "MoiTruong" -Value "Development"
```

## Build từ mã nguồn

Yêu cầu:

- .NET 9 SDK
- NSIS 3, chỉ cần khi đóng gói bộ cài
- USB token và middleware, chỉ cần khi muốn liệt kê chứng thư thật

Bốn project, chia tầng như một ứng dụng web thường:

```
ks.plugin.api            Controller, Program.cs (dựng web host, cấu hình CORS)
ks.plugin.applications   Nghiệp vụ mỏng: đọc chứng thư, ký số
ks.plugin.external       Chứng thư, phiên ký, khay hệ thống, đọc cấu hình đã cài
ks.plugin.shared         Hằng số, envelope ApiResponse
```

Chạy khi phát triển:

```powershell
cd Window/ks.plugin/ks.plugin.api
dotnet run
```

Đóng gói bộ cài:

```powershell
cd Window/ks.plugin
./dong-goi.ps1
```

Script publish plugin self-contained (máy người dùng không cần .NET runtime), gọi `makensis` đóng thành một
file thực thi duy nhất, kết quả vào `bo-cai/`.

Middleware của hãng token là phần mềm bên thứ ba, không nằm trong mã nguồn. Đặt file cài vào `vendor/bit4id/`
để bộ cài mang kèm; không có file thì vẫn đóng gói được, chỉ là bản ra không tự cài middleware.

## Bảo mật

**Không bao giờ chạm vào mã PIN.** Để middleware tự bật hộp nhập PIN; PIN đi thẳng từ bàn phím vào provider
mật mã, tiến trình plugin không thấy một byte nào. Tuyệt đối không tự vẽ ô nhập PIN — tự viết là giữ PIN trong
bộ nhớ, có thể bị dump, bị keylog, hoặc lỡ tay ghi vào log.

**Không cache danh sách chứng thư xuống đĩa.** Liệt kê lại mỗi lần: token có thể vừa được cắm hoặc vừa rút.

**Phân biệt giữ handle khoá và cache PIN.** Giữ handle một phiên là thứ khiến cả lô chỉ hỏi PIN một lần; đó
không phải cache PIN. Middleware có thể tự cache PIN theo cấu hình riêng của nó, nên code sạch là chưa đủ,
phải kiểm cả cấu hình middleware.

**Ghi log không kèm PIN và không kèm nội dung file.** Chỉ ghi thời điểm, mã công việc, vân tay chứng thư và số
lượng file.

**Không tự tải middleware từ Internet về.** Đây là phần mềm đụng tới kho khoá mật mã của cả máy và được cài
ngầm với quyền quản trị; tải một binary không rõ nguồn rồi làm vậy đúng là kịch bản một cuộc tấn công chuỗi
cung ứng cần.

Rủi ro còn lại phải nói thẳng với người dùng: mã độc có quyền người dùng trên máy đang cắm token và PIN đã
nhập thì gọi thẳng API mật mã của hệ điều hành được, không cần qua plugin. Plugin không phải lớp phòng thủ ở
đây; phòng thủ thật là rút token khi không dùng và bảo vệ máy trạm.

## Lộ trình

**Bản macOS.** Đang phát triển, viết bằng Rust. Hướng làm và các giai đoạn ghi ở
[`MacOS/README.md`](MacOS/README.md).

**Ký mã nguồn.** Binary chưa ký thì SmartScreen chặn ở lần chạy đầu; máy cơ quan thường bật chính sách không
cho bỏ qua cảnh báo, và phần mềm diệt virus xem binary chưa ký chạy nền, tự khởi động, đụng crypto token, kết
nối ra ngoài là chân dung mã độc điển hình. Trên macOS thì đây không phải lựa chọn, Gatekeeper bắt buộc.

**Bỏ cổng lắng nghe.** Hiện plugin mở cổng loopback và trang web gọi vào. Hướng đã chốt là plugin tự gọi ra
máy chủ qua WebSocket để nhận việc, không mở cổng nào cả: như vậy không trang web nào gọi được plugin, hết
chuyện mixed content, hết Private Network Access, hết xung đột cổng.

**Giám sát rút token.** Phiên ký tự đóng sau 15 phút không dùng, nhưng chưa đóng ngay khi rút token — rút giữa
lô hiện biểu hiện thành một loạt file lỗi thay vì một thông báo rõ ràng.

**Hộp PIN hiện chìm.** Plugin chạy nền không sở hữu cửa sổ nào nên hộp PIN có thể nằm sau trình duyệt. Hướng
xử lý là cho plugin chạy dạng tray app có cửa sổ ẩn rồi truyền handle cửa sổ đó vào thuộc tính CNG
`"HWND Handle"` trước khi ký.

## Đóng góp

Pull request được hoan nghênh. Với thay đổi lớn, mở issue trước để thống nhất hướng làm trước khi viết mã.

**Trước khi mở pull request**

- `dotnet build` phải sạch, không warning mới.
- Một pull request giải quyết một việc. Sửa lỗi và thêm tính năng tách thành hai.
- Đã chạy thử trên máy có USB token thật nếu thay đổi chạm vào chứng thư hoặc phép ký. Mọi phép đọc metadata
  đều có thể "đạt hết" trong khi token đã rút từ lâu, nên test không có token không chứng minh được gì.

**Quy ước mã nguồn**

- Tên class, function, variable và comment đặt bằng tiếng Việt hay tiếng Anh đều được chấp nhận, **nhưng nên
  dùng tiếng Việt**: dự án phát triển cho người Việt, nghiệp vụ vốn là tiếng Việt, để nguyên thì người đọc sau
  đỡ phải dịch ngược trong đầu. Dùng tiếng Việt thì viết không dấu, theo đúng cách mã nguồn hiện tại đang đặt
  tên.
- Trong cùng một file thì chọn một thứ tiếng và giữ nguyên. Lẫn lộn giữa hai thứ tiếng trong một file khó đọc
  hơn cả việc dùng thứ tiếng mình không thích.
- Interface đặt trong `Interfaces/`, class cài đặt trong `Implements/`, đúng tầng của nó.
- Comment đặt ở đầu function, nói *vì sao* chứ không nhắc lại *cái gì* đã hiển nhiên trong code. Chỗ nào có
  quyết định kỹ thuật không hiển nhiên thì ghi rõ lý do và hệ quả nếu làm khác.

**Giới hạn không được vượt**

Các nguyên tắc ở mục [Bảo mật](#bảo-mật) là ràng buộc thiết kế, không phải khuyến nghị. Pull request làm một
trong những việc sau sẽ bị từ chối, kể cả khi chạy đúng:

- Đọc, truyền hay lưu mã PIN ở bất kỳ dạng nào, gồm cả tự vẽ ô nhập PIN.
- Ghi danh sách chứng thư xuống đĩa.
- Ghi PIN hoặc nội dung tài liệu vào log.
- Tải middleware hoặc bất kỳ binary nào từ Internet rồi chạy.

Thay đổi nào cần vượt một trong các giới hạn trên thì mở issue bàn trước, đừng gửi mã.

## Liên hệ

- GitHub: [Ynap9](https://github.com/Ynap9)
- Email: vietcuong23122k2@gmail.com

Báo lỗi và đề xuất tính năng qua issue trên GitHub. Vấn đề liên quan tới bảo mật thì gửi email riêng, đừng mở
issue công khai.

## Giấy phép

[MIT](LICENSE) © Ynap9
