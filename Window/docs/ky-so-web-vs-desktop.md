# Ký số: KSTS (web) khác SIPPACK (desktop) ở đâu

> Đọc file này TRƯỚC khi bê bất cứ thứ gì từ `C:\Users\Admin\workspace\Sip` sang KSTS.

## Khác biệt gốc rễ

SIPPACK là desktop: BE chạy ngay trên máy người dùng (Photino in-process, same-origin), nên nó đọc thẳng ổ
đĩa, đọc thẳng Windows certificate store, và middleware token bật hộp thoại PIN ngay tại đó. KSTS là web:
**BE chạy trên server**, người dùng ngồi ở trình duyệt máy khác. Mọi dòng code SIP dựa vào "cùng máy" đều sai.

| Việc | SIPPACK (desktop) | KSTS (web) |
|---|---|---|
| Chọn file để ký | Đường dẫn thư mục trên ổ đĩa | Upload qua HTTP |
| Ảnh dấu đỏ / chữ ký tươi | Copy vào `%AppData%`, DB lưu đường dẫn | Upload MinIO, DB lưu URL |
| Đọc chứng thư số | `X509Store` của chính máy người dùng | Token ở **máy client**, server không thấy |
| Nhập PIN token | Middleware bật dialog trên máy chạy BE | Phải bật ở máy client |
| File PDF mẫu | Đường dẫn cạnh app | Phục vụ qua HTTP |

## Chứng thư số — chỗ dễ bê nhầm nhất

`docs/token-signing-tool.md` bên SIP chốt **"BE gọi `localhost:8089`"**. Kết luận đó **chỉ đúng với desktop**.
Với KSTS, `localhost` của server không phải `localhost` của người dùng — BE gọi vào đó là gọi vào chính
server, không bao giờ thấy token của ai.

Và câu hỏi "làm sao BE trên server đọc được chứng thư ở máy client" **không có lời giải, cũng không cần lời
giải**: private key nằm trong chip của token, không trích xuất được, kể cả máy đang cắm token cũng chỉ *ra
lệnh ký* chứ không đọc được key. Câu hỏi đúng phải là:

> **Ký ở client, lắp ráp ở server.** Server dựng PDF và tính hash; client dùng token ký hash; server nhận chữ
> ký về, đóng dấu thời gian và ghi file.

Cái đi qua mạng là **hash + certificate (phần public)** — không bao giờ là private key hay PIN.

Mô hình đúng cho web:

```
[Token USB] → [Agent native chạy nền ở máy client] → [Server]
                                                        ▲
                            [FE trình duyệt] ───────────┘
```

Agent là **app `.exe` cài trên máy**, hoàn toàn ngoài trình duyệt — JS trong trang và cả browser extension
đều **không bao giờ** chạm được token, vì ký bằng token phải gọi CNG/PKCS#11 là API native. Đây là lý do mọi
giải pháp ký số USB token tại VN (VGCA, VNPT, Viettel, FPT, MISA, eTax, iHTKK) đều bắt cài một app nhỏ.

Thiết kế bảo mật đầy đủ: [bao-mat-agent-ky-so.md](bao-mat-agent-ky-so.md).

## Trạng thái hiện tại của phần cert

**Plugin đã làm** ([`../ks.plugin/`](../ks.plugin/README.md)) và là nguồn ký mặc định: FE gọi plugin liệt kê chứng thư, mở phiên ký, rồi
làm người đưa thư mang `SignedAttributes` xuống và chữ ký thô lên. Xem
[luong-ky-so-hang-loat.md](luong-ky-so-hang-loat.md).

Seam đổi nguồn nằm ở `ISigningKey` phía BE, chọn bằng cấu hình `Signing:Nguon`:

| Giá trị | Implement | Dùng khi |
|---|---|---|
| bỏ trống (mặc định) | `PluginSigningKey` | Khoá nằm ở token máy người dùng — đường chạy thật |
| `store` | `StoreSigningKey` | API và token **cùng một máy Windows** — chỉ tiện cho máy dev |

`ICertificateProvider` của BE (`api/core/chung-thu-so`) vẫn đọc cert store của **máy chạy API**. Nó chỉ còn
dùng cho màn cấu hình template trên máy dev; màn ký số lấy danh sách chứng thư **từ plugin**, không qua đường
này. Deploy lên server thật thì đường này đọc cert của server, không phải của người dùng.

`ICertificateTrustValidator` được tách riêng ngay từ bây giờ vì một lý do bảo mật, không phải cho gọn: khi
cert đến từ agent, **server bắt buộc phải tự dựng chain** về Root G1/G2 đã ghim và **không được tin cờ
`IsTrusted` do client gửi** — agent chạy trên máy không tin cậy được.

## Những thứ bê thẳng được

- `SigningConstants` / `SignatureConstants` — số đo và pin CA, không dính nền tảng.
- `Cert/*.crt` (rootca, cp, rootcag2, cpg2, dcscag2) — đã có sẵn trong `ksts.be.api/Cert`.
- Nhận diện cert nằm trên token qua tên CSP/KSP (`HardwareKeyProviderMarkers`).
- Toạ độ theo **tỉ lệ 0..1** thay vì point — vốn sinh ra để một lựa chọn áp được cho nhiều khổ giấy.
- `TimestampClient` và phần dựng bản ký nối đã bê sang, giữ nguyên phía server (`external/Tsa`,
  `external/Pdf`). Đúng như dự đoán, chỉ `StoreSigningKey.Sign(hash)` và `CertificateProvider` là hai mảnh
  phải chuyển sang máy người dùng — nay là `PluginSigningKey` + `ICertificateProvider` của `ks.plugin`.
