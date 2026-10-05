# Token VGCA và driver của hãng

> **Phần 1/5** · mục lục: [README.md](README.md)

## Token Ban Cơ yếu là token bit4id

Bộ cài middleware nhúng trong `ksts.plugin/vendor/` là `bit4id_xpki_1.4.10.764-ng-user-vgca-pkimgr-bwc.exe`
(`ProductName: Universal MW`) — bit4id Universal Middleware bản OEM cho VGCA. Vì dự án **không dựa vào driver
hãng**, điều quan trọng là bên trong thẻ có gì.

## Đo thẳng trên thẻ thật — PKCS#15 chuẩn

Gọi PC/SC từ .NET 10 (gói `PCSC`) trên Windows, **chỉ lệnh đọc**:

```text
Đầu đọc : bit4id TokenME EVO v2
ATR     : 3B FF 18 00 00 81 31 FE 45 00 6B 15 0C 03 02 01 01 01 42 34 44 10 31 80 0D   ("B4D")
EF.DIR  : 4F 0A E828BD080F014E585030   50 0B "JCOP4Bit4ID"
```

Thẻ là **NXP JCOP 4 / SecID-P71** chạy applet bit4id. Bốn AID chuẩn (PKCS#15, IAS-ECC, PIV, CardOS) trả `6A82`
ở cấp gốc, nhưng cấu trúc PKCS#15 nằm **bên trong AID riêng** và đọc được sạch:

| File | Đọc được (không cần PIN) |
|---|---|
| `EF.TokenInfo 5032` | serial `23135568` · chip `NXP SecID-P71` · nhãn “VGCA Token” |
| `EF.ODF 5031` | AODF=`7001` · PrKDF=`7002` · PuKDF=`7004` · CDF=`7005` · DODF=`7006` |
| `EF.AODF 7001` | **PIN ref `0x03`**, dài 4–16, pad `0xFF`, UTF-8 · PUK ref `0x06` |
| `EF.PrKDF 7002` | **key ref `0x10`**, **RSA 3072-bit**, ID `BE72ECF5`, cần PIN, path `DF10` |
| `EF.CDF 7005` | chứng thư ID `BE72ECF5` (khớp cặp với khoá), nằm ở EF `0001` |

Chứng thư trên thẻ có serial `30D8C57549BFA5DC`, trùng với chứng thư người ký trong PDF do `kssm.be` ký.

Hệ quả:

- **Không phải reverse-engineer applet độc quyền** — PKCS#15 là ISO/IEC 7816-15, có đặc tả công khai.
- **Liệt kê chứng thư không cần PIN** — giữ được luồng “hiện danh sách trước, hỏi PIN sau” như bản Windows.
- **Phần khó nhất không cần máy Mac** — `winscard.dll` và `PCSC.framework` cùng một API, cùng bộ APDU.

## Hai ẩn số

```text
1. EF 0001 trả TLV tag 7A, dài 1482 byte, entropy cao — không phải DER trần.
   → Là NÉN, không phải mã hoá: DER gốc 1787 byte > 1482 byte trên thẻ.
   → deflate/zlib/gzip ở mọi offset đều trượt. Nghi deflate kèm preset dictionary.
   → Xác nhận 2026-09-25: deflate thô + preset dictionary ≥ 26 KB ghép từ nhiều chứng thư, xem
     plan/02-chung-thu.plan.md. Plugin dùng đường nạp .cer thay vì giải nén.
2. Chuỗi APDU ký: MSE:SET chọn key ref 0x10, rồi PSO: COMPUTE DIGITAL SIGNATURE — phải thử thật, có PIN.
```

⚠️ **OpenSC không giải được phần nén** (đối chiếu 2026-09-17): `src/libopensc/pkcs15-cert.c` đọc chứng thư DER
trần, không có bước giải nén nào. Đừng coi “nhúng OpenSC” là cách né bài toán này. Đường đúng: đã có **cả bản nén
lẫn bản gốc** ⇒ dò thuật toán bằng bản rõ đã biết, offline trên Windows.

⚠️ **Sai PIN 3 lần là khoá chết token.** Khi bắt đầu test ký phải gửi `VERIFY` (`00 20`): chặn cứng số lần thử ở
tầng code **trước khi** chạm thẻ, test bằng **token dự phòng**. Tránh xa `UPDATE BINARY`, `CHANGE REFERENCE DATA`
và lệnh GlobalPlatform `DELETE`/`INSTALL`.

## Ban Cơ yếu đã có driver macOS — nhưng không có arm64

`VGCA_VCTKInstaller.pkg`: ký bằng Developer ID Installer của **Mobile-ID Technologies and Services JSC** (Team ID
`XGYQ4M8WLZ`), đóng ngày **29/06/2023**, macOS tối thiểu 10.15.

```text
/Applications/VGCA VCTKManager.app          com.vgca.VCTKManager   (LSUIElement)
├─ Contents/PlugIns/VCTKToken.appex         CryptoTokenKit token extension (com.apple.ctk-tokens, smartcard)
├─ Contents/Library/LoginItems/VCTKManagerHelper.app
└─ Contents/Library/LoginItems/VCTKManagerNotify.app
```

- `postinstall` cài 4 chứng thư vào System keychain; SHA-256 **trùng khít** các pin trong `SignatureConstants`
  của `kssm.be` — kiểm chéo độc lập rằng dự án đã ghim đúng.
- ⚠️ Cả bốn Mach-O cùng thư viện kèm theo là **`THIN x86_64`**. Token extension được nạp vào `ctkd` arm64 gốc nên
  Rosetta không gánh được ⇒ driver chính thức **nhiều khả năng không chạy trên Mac chip M**. Gói ba năm chưa cập
  nhật — rủi ro tổ chức, đội không sửa được.
- Gói kèm chứng thư FPT-CA, Vietnam National Root CA (NEAC) và ba CA của Lào; binary dùng `TKSmartCard` của Apple.

## OpenSC — công cụ và mã tham chiếu

| | VGCA VCTK | OpenSC |
|---|---|---|
| Kiến trúc | `x86_64` thuần | universal (arm64 + x86_64) từ 0.22.0 |
| Bản mới nhất | 1.0 — 29/06/2023 | 0.27.1, phát triển liên tục |
| Giao diện | CryptoTokenKit | PKCS#11 + CryptoTokenKit + minidriver |
| Giấy phép | — | LGPL-2.1, link động hợp lệ cho app đóng |

Phép thử chưa chạy, làm được ngay trên Windows với token đang có:

```bash
opensc-tool --list-readers
opensc-tool --atr
pkcs15-tool --dump
pkcs15-tool --list-certificates
pkcs15-tool --read-certificate 01 --out cert.pem
```

| Kết quả | Nghĩa là | Hướng đi |
|---|---|---|
| Đọc ra `cert.pem` hợp lệ | ✅ Tốt nhất | Nhúng OpenSC vào app — không viết driver thẻ, không bẻ phần nén |
| `--dump` thấy khoá/chứng thư nhưng đọc cert trượt | 🔶 Gần | Tự viết driver, tra phần nén ở `src/libopensc/pkcs15-cert.c` |
| OpenSC không nhận thẻ | 🔶 Vẫn làm được | Tự viết trọn driver; hằng số đã có ở bảng trên, còn bẻ phần nén và chốt APDU ký |

`--dump` thấy khoá/chứng thư nhưng đọc cert trượt là kết quả **dự đoán được** sau khi đã biết `pkcs15-cert.c`
không giải nén. Kể cả không nhúng, OpenSC vẫn đáng dùng làm công cụ gỡ lỗi (`opensc-explorer`, `pkcs11-tool`).

## Nhúng OpenSC — phương án nằm giữa

Nhúng `opensc-pkcs11.dylib` vào `<App>.app/Contents/Frameworks/`, nạp qua Pkcs11Interop: người dùng tải một app,
chạy một app, không cài gì thêm.

| | Tự viết driver | Nhúng OpenSC | Bắt cài driver hãng |
|---|---|---|---|
| Người dùng phải cài | không | **không** | có |
| Apple Silicon | tự lo, dễ | **universal sẵn** | ❌ VGCA chỉ `x86_64` |
| Phải bẻ phần nén cert | có | ⚠️ chưa chắc — `pkcs15-cert.c` không giải nén | không |
| Công sức | cao | **thấp** | thấp |
| Rủi ro dài hạn | tự chủ hoàn toàn | phụ thuộc dự án còn sống | phụ thuộc Ban Cơ yếu |

> **Tiếp:** [02-ky-so-da-nang.md](02-ky-so-da-nang.md) — một app Rust đã ký token trong nước trên Apple Silicon.
