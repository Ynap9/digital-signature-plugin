# Luồng ký số hàng loạt — quyết định đã chốt

> Cập nhật 2026-08-14. Đọc sau [ky-so-web-vs-desktop.md](ky-so-web-vs-desktop.md). File này mô tả **cái đang
> chạy**; phần nghiên cứu chưa thi công gom ở cuối và ở [bao-mat-agent-ky-so.md](bao-mat-agent-ky-so.md).

## Phân vai

| Việc | Ở đâu |
|---|---|
| Mở lô, nhận file (tải lên hoặc trỏ vào thư mục có sẵn trên kho) | Server |
| Đọc template từ DB, dựng PDF có placeholder `/ByteRange` | Server |
| Tính hash 2 dải ByteRange, dựng `SignedAttributes` | Server |
| **`sign(SignedAttributes)` bằng khoá token** | **Plugin máy user** |
| Gọi TSA, ghép CMS vào `/Contents`, đẩy bản ký lên kho | Server |
| Dựng chuỗi tin cậy về Root G1/G2 đã ghim | Server |

Thao tác mật mã **không thể** chạy trên server: khoá bí mật nằm trong chip token, không trích xuất được. Qua
mạng chỉ có `SignedAttributes` + chữ ký thô + chứng thư phần công khai; PIN và khoá không bao giờ rời máy user.

## Đường truyền chữ ký — TRANG WEB LÀM NGƯỜI ĐƯA THƯ

Đây là cái **đang chạy**. Trang web đang mở là người trung chuyển giữa máy chủ và token:

```
1. FE  -> plugin  ky-so/mo-phien {thumbprint}          <- hộp PIN bật ĐÚNG MỘT LẦN, plugin giữ handle khoá
2. FE  -> BE      lo-ky/{id}/mo-phien {chungThuBase64} <- chỉ phần CÔNG KHAI
3. FE  -> BE      lo-ky/{id}/bat-dau {thumbprint}
4. lặp: BE giữ lời gọi lo-ky/{id}/cho-ky tới khi có việc, trả tối đa 8 SignedAttributes một đợt
        FE -> plugin ky-so/ky   -> chữ ký thô
        FE -> BE     lo-ky/{id}/chu-ky
5. xong -> plugin ky-so/dong-phien + BE lo-ky/{id}/dong-phien
```

Chỗ hẹn giữa hai bên là `IHangDoiKy` (`external/Signing`): tiến trình ký bỏ `SignedAttributes` vào đó rồi
**ngủ chờ**, phía máy người dùng lấy ra ký và nộp chữ ký về đánh thức nó dậy. Nguồn ký cắm qua `ISigningKey`
nên đổi được bằng cấu hình:

```jsonc
"Signing": { "Nguon": "" }        // bỏ trống  -> PluginSigningKey (mặc định, khoá ở máy người dùng)
"Signing": { "Nguon": "store" }   // "store"   -> StoreSigningKey  (đọc cert store của MÁY CHẠY API)
```

Khoá `Signing` hiện **không có** trong `appsettings.json` — vắng mặt nghĩa là chạy `PluginSigningKey`. Thêm
khoá đó chỉ khi muốn quay về `store`, mà `store` chỉ dùng được khi API và token nằm trên **cùng một máy
Windows**: tiện cho máy dev, sai hoàn toàn khi deploy lên server thật.

### Ba điều kiện làm nó nhanh, thiếu một là hỏng

1. **Gom yêu cầu theo đợt** — `SigningQueueConstants.SoYeuCauMoiDot = 8`. Token ký tuần tự nên độ trễ đường
   truyền cộng thẳng vào từng file; gom 8 thì chia đều cho 8.
2. **Giữ lời gọi lấy việc** — `GiayChoLayViec = 25` giây, không hỏi theo nhịp. Hỏi mỗi 500 ms là cộng tới
   500 ms cho mỗi file, lô 5000 file thành gần một tiếng. Con số 25 giây là để không chạm giới hạn thời gian
   của proxy đứng trước máy chủ.
3. **KHÔNG khoá tuần tự phép ký ở máy chủ.** Còn khoá thì mỗi lúc chỉ một yêu cầu bay đi và điều 1 vô nghĩa.
   Token vẫn ký lần lượt — việc xếp hàng do chính plugin lo.

`GiaySongCuaYeuCau = 120`: một lượt ký không ai lấy đi trong 120 giây thì chết hẳn và file tính lỗi, thay vì
giữ một luồng ký treo mãi.

⚠️ Bẫy đã sập: hạn 120 giây được dựng bằng **linked token** với token của lô, nên lô bị dừng cũng bật đúng cái
token đó. Bản đầu để callback ném thẳng `TimeoutException` cho cả hai trường hợp ⇒ file đang chờ chữ ký bị ghi
**Lỗi** thay vì trả về `Cho`, và vì lấy việc chỉ lọc `Cho` nên đúng 8 file đang dở **không bao giờ ký tiếp
được** — kể cả khi người dùng bấm dừng đúng cách. Phải hỏi `cancellationToken.IsCancellationRequested` trong
callback để phân biệt *lô bị dừng* (huỷ lượt ký) với *lượt ký quá hạn* (ném timeout).

### Cái giá phải trả, phải nói rõ với người dùng

⚠️ **Đóng tab là lô dừng** — mất người đưa thư. File đã ký giữ nguyên và vẫn hợp lệ; bấm bắt đầu lại thì chạy
tiếp từ file kế tiếp (lấy việc luôn lọc `TrangThai = Cho`).

**Dừng và Huỷ là hai thao tác khác nhau**, chọn bằng `KieuDungLo` truyền vào `IKySoRunner.Dung`: dừng thì trả
file dở về `Cho` và **giữ** bản nguồn để còn ký tiếp; huỷ thì lô không chạy lại nữa nên bản nguồn được dọn.
Bản đã ký **không bao giờ bị xoá** ở cả hai đường — nó nằm ở thư mục dùng chung theo khoá tuyển sinh, tên file
là số CCCD, nên xoá theo lô là xoá trúng bản của lô khác. Bảng đối chiếu đầy đủ ở
`.claude/contracts/lo-ky.contract.md` bên repo `ksts`.

⚠️ Trạng thái cuối của lô do **tiến trình nền** chốt trong `KetThucLoAsync`, mà nó chạy **sau** khi service đã
ghi DB. Không truyền lý do dừng xuống là nó ghi đè trạng thái vừa ghi — bấm Dừng lại hiện ra "đã huỷ".

⚠️ **Mở lại màn hình khi lô đang chạy thì chưa nối lại được vòng đưa thư.** Màn hình thấy đúng tiến độ qua
`lo-ky/dang-chay`, nhưng không ai mang chữ ký đi nên các lượt ký hết hạn sau 120 giây và file tính lỗi. Chưa
làm nút nối lại phiên vì việc đó phải hỏi PIN lần nữa — cần quyết định về mặt giao diện trước.

## Vòng đời một lô

1. `POST lo-ky {templateId}` → mở lô rỗng, server phát luôn `taiToken` dùng cho đường tải zip.
2. Nạp file, **một trong hai đường**:
   - `POST lo-ky/{id}/them-file` — tải lên từng đợt (FE gửi ~50 file/đợt, trần server 200). Server lưu vào
     `lo-ky/{id}/nguon/`, **object key do server đặt**, khử trùng theo tên file trong phạm vi lô.
   - `POST lo-ky/{id}/them-tu-kho {duongDan}` — trỏ vào **thư mục có sẵn trên kho**, lô chỉ ghi lại object key
     đang có. Bỏ hẳn khâu tải lên: không chép byte nào, không nhân đôi dung lượng. Đây là đường dùng thật sau
     khi dựng giấy báo, vì bản chưa ký đã nằm sẵn trên kho.
3. `POST lo-ky/{id}/mo-phien {chungThuBase64}` — server đọc chứng thư, **tự dựng chuỗi tin cậy**
   (`ICertificateTrustValidator`), rồi mở phiên trong hàng đợi.
4. `POST lo-ky/{id}/bat-dau {thumbprint}` — `KySoRunner` chạy nền, 8 file song song, mỗi luồng tự rút việc.
5. FE chạy vòng `cho-ky` ⇄ `chu-ky` cho tới hết lô.
6. `GET lo-ky/{id}/zip?token=…` — nén **ngay lúc tải**, kéo từng bản từ kho ra rồi ghi thẳng vào luồng gửi đi.

Bản đã ký được ghi **thẳng** vào thư mục dùng chung của kho (`LoKyConstants.GetKhoDaKyKey`), một thao tác cho
một file. Bản trước ghi vào chỗ làm việc của lô rồi mới chép sang: hai vòng đi-về tới kho cho mỗi file, mà kho
nằm ngoài mạng nên khoản đó cộng thẳng vào thời gian cả lô (migration `BoBuocDayLenKhoRieng` bỏ hẳn bước này).

Lô chạy trọn thì dọn `lo-ky/{id}/nguon/`; lô bị huỷ **không** dọn, vì còn phải ký tiếp từ file dở.

## Trình tự một file

```
Server  tải bản nguồn từ kho
        dựng PDF theo template → hash 2 dải ByteRange → SignedAttributes
   ↓  qua hàng đợi, trang web mang đi
Plugin  sign(SignedAttributes) bằng handle đã mở
   ↓  chữ ký thô
Server  CMS detached + signingCertificateV2 → TSA (thử lại tối đa 3 lần)
        → gắn token TSA làm unsigned attribute → ghi vào /Contents → đẩy bản ký lên kho
```

**Fail-closed**: TSA hỏng sau 3 lần thử ⇒ file đó hỏng. Không bao giờ phát hành chữ ký thiếu dấu thời gian.

Lỗi một file **không** làm dừng lô — chỉ ghi `LyDoLoi` rồi đi tiếp. Riêng **không tải được ảnh chữ ký tươi**
của template thì dừng cả lô: template đã khai có chữ ký tươi nghĩa là người dùng đang chờ nó xuất hiện trên
giấy, phát hiện ra 5000 tờ thiếu chữ ký sau khi ký xong thì phải ký lại toàn bộ.

⚠️ Bẫy đã sập một lần: bấm Huỷ thì các luồng ném `OperationCanceledException`; để nó thoát khỏi `Task.WhenAll`
là lô bị ghi thành **Lỗi** thay vì **Huỷ**. Phải nuốt riêng loại đó rồi mới chốt trạng thái.

## Mặt chữ ký đi theo CỜ TEMPLATE

Hai cờ **độc lập**, không phải "kiểu A/B" loại trừ nhau: FE là hai checkbox riêng, DB là hai cột bool riêng.

| Cờ | Việc |
|---|---|
| `HienThiChuKySo` | Vẽ khối chữ ký số 2 dòng tại position `ChuKy` |
| `NhoiChuKySoVaoAnh` | Widget chữ ký trùm lên **ảnh chữ ký tươi và con dấu** ⇒ bấm vào ảnh ra bảng thông tin ký |

Bật cả hai ⇒ **một** chữ ký, nhiều widget `/Kids`. Tắt cả hai ⇒ chữ ký **vô hình**, vẫn hợp lệ. Ảnh chữ ký
tươi luôn được vẽ nếu template có; cờ nhồi chỉ quyết định nó là **widget chữ ký** hay **annotation Stamp**
thường.

**Con dấu không bao giờ vẽ ở luồng ký** — nó đã có sẵn trong trang từ bước dựng giấy báo. Hệ quả đã chấp nhận:
chữ ký tươi vẽ sau nên nằm **trên** dấu, ngược thứ tự lớp so với bản giấy; ảnh dấu nền trong suốt nên vẫn đọc
được cả hai. Đừng "sửa" bằng cách vẽ lại dấu ở tầng ký — sẽ thành hai con dấu trên một tờ.

## Template quyết định gì

| Trường | Dùng để |
|---|---|
| `TemplatePosition` (tỉ lệ 0..1) | Vị trí khối chữ ký số và ảnh chữ ký tươi, áp được mọi khổ giấy |
| Ảnh chữ ký tươi (kho object) | Nạp **một lần** cho cả lô lúc mở phiên, không tải lại theo từng file |
| `LyDoKy` / `NoiKy` | Vào signature dictionary |
| `HienThiChuKySo` / `NhoiChuKySoVaoAnh` | Mặt chữ ký, xem mục trên |
| `DoDamChuKyTuoi` / `DoDayNetChuKyTuoi` | Độ đậm và độ dày nét ảnh chữ ký tươi |
| `MauChuKySo` / `MauChuKyTuoi` | Màu chữ khối ký số và màu mực ảnh chữ ký tươi; **đen = giữ nguyên** |
| `KyDe` | Chốt cửa: tắt thì file nguồn **đã có chữ ký** bị đánh trượt (`1148`) thay vì ký thêm |

`KyDe` được kiểm **ngay sau khi tải bản nguồn**, trước cả bước dựng PDF (`IPdfSignatureInspector`): file đã ký
mà template chưa bật cờ thì hỏng riêng file đó, cả lô vẫn chạy tiếp. Mặc định là **tắt** nên template cũ tự
nhiên được bảo vệ khỏi việc ký chồng ngoài ý muốn.

## Chỗ lưu file

| Tiền tố | Nội dung |
|---|---|
| `lo-ky/{loKyId}/nguon/` | Bản nguồn của lô nhận file **tải lên**; dọn khi lô chạy trọn |
| `GiayBaoTrungTuyen/{khoá}/GiayBaoTrungTuyen/` | Bản **chưa ký** do khâu dựng giấy báo đẩy lên |
| `GiayBaoTrungTuyen/{khoá}/GiayBaoTrungTuyenDaKySo/` | Bản **đã ký**, cùng tên file (số CCCD) với bản gốc |

Không giữ file trên đĩa máy chạy API — API có thể chạy nhiều instance hoặc trong container không có ổ bền
vững. Chi tiết: `.claude/docs/luu-tru-minio.md` bên repo `ksts`.

## Số đo thật

### Trọn đường, kho `s3-2.huce.edu.vn`, giấy báo thật 911 KB, 24 file/vòng

| Đồng thời | Tải nguồn | Dựng + ký | TSA | Đẩy bản ký | **ms/file** | **5000 file** |
|---|---|---|---|---|---|---|
| 1 | 135 ms | 55 ms | 11 ms | 115 ms | 317 | 26 phút |
| 4 | 395 ms | 48 ms | 21 ms | 211 ms | 183 | 15 phút |
| **8** | 913 ms | 46 ms | 21 ms | 205 ms | **163** | **13 ph 35** |
| 16 | 1.839 ms | 49 ms | 23 ms | 234 ms | 169 | 14 phút |

**Nút thắt là BĂNG THÔNG tới kho.** Cột tải nguồn tăng gần đúng tỉ lệ với số luồng — đường truyền đã bão hoà,
thêm luồng chỉ chia nhỏ cùng một lượng băng thông. Vì vậy 16 luồng **chậm hơn** 8 luồng và
`LoKyConstants.SoFileSongSong = 8` đang là đúng mức.

Mỗi file đi qua ~1,83 MB ⇒ ở 163 ms/file là **~11 MB/s ≈ 90 Mbps**; cả lô 5000 tờ là **~9 GB** qua máy chạy
API. Dùng đường `them-tu-kho` thì không có thêm lượt tải lên nào nữa.

⚠️ Ba điều kiện của con số 13 phút, thiếu một là sai:

1. **Đo với chứng thư trong cert store máy chạy API** (`Signing:Nguon = store`), phép ký RSA tại chỗ dưới 1 ms.
   Lắp token thật vào thì token ký **tuần tự**: `T ≈ 200 ms` một lượt là riêng phần ký đã 5000 × 0,2s =
   **16 ph 40**, không rút ngắn được bằng thêm luồng. **`T` chưa đo được** — đo bằng thời gian một đợt
   `ky-so/ky` chia số yêu cầu khi có token trong tay.
2. **Đo từ máy dev.** API chạy cùng hạ tầng với kho thì hai chặng mạng rút ngắn nhiều; deploy API xa kho là
   9 GB đi vòng qua Internet hai lần.
3. Chặng **"Dựng + ký" 46 ms** không phình theo số luồng nên khoá quanh PDFsharp chưa thành nút thắt.

### Phần CPU thuần (file mẫu 870 KB, 30 vòng)

| Chặng | Trung bình |
|---|---|
| Prepare (chưa có ảnh chữ ký tươi) | 4,7 ms |
| BuildSignedAttributes | 0,4 ms |
| Ký RSA tại chỗ | 0,4 ms |
| Assemble CMS | 0,0 ms |
| Ghi CMS vào `/Contents` | 0,3 ms |

Prepare giờ là 46 ms vì mỗi file phải nạp ảnh PNG 143 KB vào PDF và vẽ 9 lớp nong nét. Hằng số
`TsaTimeoutSeconds = 180` ghi "đo thực 2s-131s" là số bê từ SIP, **sai với thực tế**: TSA chỉ 11–23 ms.

- CMS hoàn chỉnh (chữ ký + chain + token TSA): **~5,1 KB** — chỗ trống 32 KB thừa gấp 6 lần, an toàn.
- Token TSA thật của `tsa.ca.gov.vn`: **~3,75 KB**.
- Ký chồng lên file đã ký: **cả hai chữ ký đều còn hợp lệ**, byte bản cũ giữ nguyên tuyệt đối.

## Tối ưu đã làm

1. **Phiên ký mở một lần cho cả lô** (`KySoRunner.MoPhienAsync`): chứng thư, chuỗi chứng thư, cấu hình template
   và ảnh chữ ký tươi nạp đúng một lần. Trước đó mỗi file đều truy vấn lại template và tải lại ảnh từ kho.
2. **8 file chạy song song**, mỗi luồng tự rút việc từ hàng đợi; **nhận việc có khoá** (`_khoaNhanViec`) để hai
   luồng không nhận trúng cùng một file.
3. **Bỏ khoá tuần tự quanh phép ký** — token tự xếp hàng bên plugin.
4. **Đếm kết quả bằng một câu lệnh cộng dồn** (`ExecuteUpdateAsync`) thay cho hai câu `COUNT` quét cả bảng sau
   mỗi file; nhiều luồng cùng đếm còn ghi đè kết quả của nhau.
5. **Khoá quanh PDFsharp** (`PdfAppearanceBuilder.Draw`) vì nó giữ bộ nhớ đệm font dùng chung cho cả tiến
   trình. Đo lại: phần nằm trong khoá vẫn ngắn, chưa phải nút thắt.
6. **Ghi bản ký thẳng vào thư mục dùng chung**, bỏ bước đẩy lên kho riêng.
7. **Zip dựng ngay lúc tải**, tải trước 8 file trong khi đang ghi file hiện tại; máy chủ không giữ file nén nào
   trên đĩa.

---

# Nghiên cứu — tối ưu sau

Phần dưới đây **chưa thi công**. Giữ lại vì đã cân nhắc kỹ và sẽ cần khi nghiệp vụ đổi.

## Topology B — plugin tự gọi ra server qua WSS

```
Browser (FE) ──HTTPS──> Server <──WSS outbound── Plugin ──> Token
                       (hàng đợi job)
```

Plugin **không mở cổng nào**: website khác không gọi được plugin, hết mixed content, hết Private Network
Access, hết xung đột port. FE chỉ tạo lô và xem tiến độ — **đóng tab thì lô vẫn ký tiếp**.

**Vì sao chưa làm**: cùng một mức bảo mật với đường đang chạy nhưng nhiều việc hơn hẳn (pairing UX + hàng đợi
job phía server + tự kết nối lại), mà bản chạy thật cần kịp mùa tuyển sinh.

**Khi nào nên làm**: khi nghiệp vụ cần **đóng tab mà lô vẫn chạy** — đó là thứ duy nhất đường hiện tại không
làm được. Đổi sang nó **không phải viết lại phần lõi**: `IHangDoiKy` và `PluginSigningKey` giữ nguyên, chỉ thay
lớp vận chuyển (chính vì thế mà `IHangDoiKy` không biết ai là người đưa thư).

## Job ticket server ký + WYSIWYS

Thiết kế đầy đủ ở [bao-mat-agent-ky-so.md](bao-mat-agent-ky-so.md) §3 và §5. Tóm tắt: server phát ticket có
chữ ký, public key ghim cứng vào plugin lúc build; ticket khoá vào **một** cert + **một** user + **đúng** số
lần ký, kèm `nonce` chống replay. Plugin nhận cả PDF đã prepare, **tự tính digest** hai dải `/ByteRange` rồi so
với `messageDigest` — server nói dối được về *tên* file nhưng không nói dối được về *nội dung* đang ký.

**Hiện tại chưa có mảnh nào**: plugin ký bất cứ `SignedAttributes` nào trang web đưa xuống, trong phạm vi một
phiên do chính người dùng mở bằng PIN. Đây là mức bảo mật của hầu hết plugin ký số ở VN, nhưng **không** chặn
được T3 (server bị chiếm) — cần nói thẳng khi bàn giao.

## Còn phải chốt

- **`T` — thời gian một lượt ký qua token thật.** Là con số quyết định thời lượng cuối cùng của lô. Đo bằng
  thời gian một đợt `ky-so/ky` chia số yêu cầu ngay khi có token.
- **Nút nối lại phiên** khi mở lại màn hình giữa lô — vướng ở chỗ phải hỏi PIN lần nữa.
