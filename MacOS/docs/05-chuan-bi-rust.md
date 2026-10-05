# Chuẩn bị Rust cho plugin

> **Phần 5/5** · trước: [04-phuong-an.md](04-phuong-an.md) · mục lục: [README.md](README.md)

Máy dev Windows **chưa cài Rust** (không có `rustup`, `cargo` — kiểm 2026-09-17). Plugin hiện tại ~1.400 dòng C#,
bản Rust phải giữ **nguyên hợp đồng** ở [../../Window/docs/plugin-ky-so.contract.md](../../Window/docs/plugin-ky-so.contract.md)
để FE và hai backend không sửa dòng nào — trừ chỗ PIN, xem [02-ky-so-da-nang.md](02-ky-so-da-nang.md).

## Từ C# sang Rust

| Trong `ksts.plugin` | Trong Rust | Điều khác cần nắm |
|---|---|---|
| 4 project `.csproj` trong `.sln` | 4 crate trong một Cargo workspace | Chiều phụ thuộc được trình biên dịch ép, giống `external → shared` |
| `ICertificateProvider`, `ISigningSession` | `trait` | Không DI container: dựng một lần trong `main`, gói `Arc<dyn Trait>` vào state của axum |
| `lock (_khoa)` trong `SigningSession` | Một luồng OS riêng giữ kết nối thẻ, nhận việc qua channel | Lời gọi PC/SC **chặn luồng**; gọi thẳng trong handler async là treo worker tokio. Luồng riêng vừa tuần tự như `lock`, vừa là chỗ duy nhất chạm thẻ |
| `IDisposable` · `CloseLocked` | `Drop` | Nhả kết nối thẻ khi ra khỏi phạm vi; PIN bọc `zeroize` để bị xoá khỏi RAM |
| Exception + `OkException` | `Result<T, E>` + toán tử `?` | Lỗi là giá trị trả về; mã `SW` của thẻ (`6982`, `63Cx`) thành `enum` rồi `match` |
| `ApiResponse`, enum ra số | `serde` + `serde_repr` | `#[serde(rename_all = "camelCase")]` giữ đúng tên trường của hợp đồng |
| `PluginConstants.OriginMacDinh` | `const` trong crate shared | Giống hệt: ghim lúc build, bản phát hành không kèm file cấu hình |
| `SignWithHandle` — `SignData(SHA-256, Pkcs1)` | `sha2` băm + dựng `DigestInfo` + APDU `PSO:CDS` | Thẻ không băm hộ; phải khớp đúng thuật toán máy chủ lắp CMS |

## Bộ crate tối thiểu

Phiên bản tra trên crates.io ngày 2026-09-17. Khoảng 20 crate trực tiếp, so với 153 của “Ký số đa năng”.

```text
chạm thẻ    pcsc 2.9 · (dự phòng) nusb 0.2
đọc thẻ     der 0.8 · x509-cert 0.3 · flate2 1.1 (dò phần nén 7A)
mật mã      sha2 · rsa 0.9 (chỉ để tự kiểm chữ ký thẻ trả về) · zeroize 1.9
HTTP        axum 0.8.9 · tokio 1.53 · tower-http 0.7 (CORS)
HTTPS       axum-server 0.8 · rustls 0.23 · rcgen 0.14
macOS       security-framework 3.7 (trust) · tray-icon 0.25 · objc2 0.6
dữ liệu     serde 1.0 · serde_json 1.0 · serde_repr 0.1 · base64 0.23
lỗi · log   thiserror 2.0 · tracing 0.1
```

`pcsc` 2.9.0 gọi `winscard.dll` trên Windows, `PCSC.framework` trên macOS, pcsclite trên Linux; context PC/SC chuyển
sang luồng khác được. Bản cuối phát hành 2024-12 nhưng API PC/SC đã đứng yên nhiều năm nên không phải tín hiệu xấu.

## Bẫy riêng của bài toán này

- ⚠️ **Vòng lặp giao diện phải chiếm luồng chính.** Trên macOS, `tray-icon`, `winit` và mọi cửa sổ AppKit phải tạo và
  chạy trên luồng chính; tokio + máy chủ HTTP chạy luồng phụ, nói chuyện qua channel. Làm ngược là sập lúc tạo icon.
- ⚠️ **Lệch họ trait mật mã.** `rsa 0.9` đi cùng `digest 0.10` (`sha2 0.10`); `sha2 0.11` dùng `digest 0.11`. Trộn là
  lỗi trait không khớp rất khó đọc — ghim cùng một họ, đừng “nâng” riêng `sha2` lên 0.11.
- ⚠️ **Sửa bundle là phá chữ ký ad-hoc.** Trình liên kết tự ký ad-hoc trên Apple Silicon, nhưng chép vào `.app`, sửa
  `Info.plist` hay gộp bằng `lipo` đều làm chữ ký mất hiệu lực — luôn `codesign` **sau cùng**.
- ⚠️ **Hai PC/SC context, không phải một.** `SCardGetStatusChange` chờ rút thẻ là lời gọi chặn dài; dùng chung context
  với luồng ký thì lượt ký xếp hàng sau lượt chờ. Mở context riêng như `pcsc-token-watcher` của “Ký số đa năng”.
- ⚠️ **Không thử PIN thật trên token chính.** Mọi bước có `VERIFY` chạy trên token dự phòng, sau khi đã hỏi lượt thử.

## Thứ tự học — mỗi bước ra một kết quả đo được

| Bước | Ở đâu | Việc | Mắt xích ([03](03-kha-thi-rust.md)) |
|---|---|---|---|
| **R1** | Windows | Cài `rustup`; ownership, `Result`, `enum`/`match`, `trait`. Bài tập: parse TLV/BER của EF.ODF, AODF, PrKDF, CDF đã dump, ra đúng PIN ref `0x03`, key ref `0x10` | — |
| **R2** | Windows | Port phép đo chỉ-đọc sang `pcsc`: đầu đọc, ATR, `SELECT`/`READ BINARY`. Song song dò phần nén `7A` bằng cặp nén/gốc. Chưa chạm PIN | 2, 3 |
| **R3** | Windows · token dự phòng | Hỏi lượt thử, chặn khi ≤ 1; `VERIFY` → `MSE:SET` → `PSO:CDS`; kiểm chữ ký bằng khoá công khai; đo `T` so với bản .NET. **Qua bước này là chắc ký được chứng thư Ban Cơ yếu bằng Rust** | 4, 5 |
| **R4** | Windows | Máy chủ axum đúng hợp đồng: `trang-thai`, `chung-thu-so`, `ky-so/mo-phien`, `ky`, `dong-phien`, envelope `{ status, data, code, message }`; chạy với màn ký số thật trên Chrome | — |
| **R5** | Mac | Chạy lại R2 xem đầu đọc có hiện; HTTPS loopback + cài trust; menu bar; `.app` universal, ký ad-hoc, chép USB; thử Safari trên cả Mac Intel lẫn chip M | 1, 6, 7 |

Hàng rào cho API localhost ([02-ky-so-da-nang.md](02-ky-so-da-nang.md)) — kiểm Host, Origin allowlist, hỏi quyền theo
website — làm cùng R4, không để sau: đó là chỗ plugin mở cổng cho mọi tab trình duyệt.
