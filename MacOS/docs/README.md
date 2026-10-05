# Ký số trên macOS

> 🔬 **NGHIÊN CỨU — chưa thi công.** Khảo sát 2026-09-08, bổ sung 2026-09-17. Bộ tài liệu này là bản
> Markdown của báo cáo khảo sát (HTML):
> https://claude.ai/code/artifact/55928828-28b3-4a69-9126-41fa0042b1cd. Đọc cùng
> [../../Window/docs/plugin-ky-so.contract.md](../../Window/docs/plugin-ky-so.contract.md) và
> [../../Window/docs/ky-so-plugin.plan.md](../../Window/docs/ky-so-plugin.plan.md).

Câu hỏi: plugin ký số (`ksts.plugin` — trong repo này là [`../../Window/ks.plugin`](../../Window/ks.plugin/README.md), dùng chung cho `ksts.be` và `kssm.be`) có chạy được trên Mac không, nếu
làm mới thì stack nào, và **ký được chứng thư Ban Cơ yếu bằng Rust trên macOS không** — ưu tiên giải pháp miễn
phí.

## Kết luận nhanh

| Câu hỏi | Trả lời |
|---|---|
| Cài plugin hiện tại lên Mac? | ❌ Không — khoá ở `net9.0-windows` + WinForms + Windows certificate store |
| Dựa vào driver của hãng? | ❌ Không — quyết định dự án; driver VGCA cho Mac là `x86_64` thuần, không nạp được trên chip M |
| Tự nói chuyện với token? | ✅ Được — token VGCA là **PKCS#15 chuẩn**, đọc sạch không cần PIN (đo trên thẻ thật) |
| Ký chứng thư Ban Cơ yếu bằng Rust trên Mac? | ✅ **Được về nguyên lý**, 0 đồng tới bản nội bộ — **chưa ký thật**, còn 3 phép đo |
| Một bản cho cả Mac Intel lẫn chip M? | ✅ Universal binary, tối thiểu đề xuất **macOS 11** |

## Thi công bằng Rust

Hướng đã chọn: **Rust**. Kiến trúc ở [architecture/](architecture/README.md), kế hoạch từng bước chờ duyệt ở
[plan/](plan/README.md).

## Mục lục — đọc theo thứ tự

| Phần | Nội dung |
|---|---|
| [01-token-vgca.md](01-token-vgca.md) | Token VGCA đo trên thẻ thật, gói driver macOS của Ban Cơ yếu, OpenSC, phần chứng thư bị nén |
| [02-ky-so-da-nang.md](02-ky-so-da-nang.md) | Mổ app Rust “Ký số đa năng”: crate, luồng chạm thẻ, hỏi PIN, bảo vệ localhost, TLS, phát hành |
| [03-kha-thi-rust.md](03-kha-thi-rust.md) | Bảy mắt xích để ký chứng thư Ban Cơ yếu bằng Rust, phát hành miễn phí, phạm vi Mac Intel / chip M |
| [04-phuong-an.md](04-phuong-an.md) | Phụ thuộc Windows của plugin hiện tại, chi phí ẩn, bốn phương án .NET/Rust, lộ trình, đường vòng |
| [05-chuan-bi-rust.md](05-chuan-bi-rust.md) | Rust cho người viết C#: ánh xạ khái niệm, bộ crate, bẫy, thứ tự học R1–R5 |

## Ba phép đo còn thiếu

1. **macOS có nhận đầu đọc `bit4id TokenME EVO v2`** — cần máy Mac.
2. **Giải nén chứng thư thẻ `7A`** — làm offline trên Windows, đã có cả bản nén lẫn bản gốc.
3. **`VERIFY` + ký thật** (`MSE:SET` key `0x10` → `PSO:CDS`) — làm trên Windows, **bằng token dự phòng**.

⚠️ Mọi phép đo trên thẻ tới nay **chỉ là lệnh đọc**. Sai PIN 3 lần là khoá chết token — xem
[01-token-vgca.md](01-token-vgca.md).

## Nguồn

Phần lớn kết luận đến từ **đo trực tiếp**, không từ tài liệu:

- Mã nguồn `ksts.plugin` v1.0.1 và bộ cài `vendor/bit4id/bit4id_xpki_1.4.10.764-ng-user-vgca-pkimgr-bwc.exe`.
- `VGCA_VCTKInstaller.pkg` — bung XAR → cpio, đọc `Distribution`, `PackageInfo`, `Info.plist`, script cài, đối
  chiếu SHA-256 chứng thư, kiểm kiến trúc Mach-O của 130 tệp.
- `Ky-so-da-nang-macOS-Apple-Silicon.dmg` — bung UDIF/zlib → HFS+, trích chuỗi, 153 đường dẫn crate có phiên bản,
  bố cục workspace, `Info.plist`, danh tính chữ ký mã, thông báo lỗi về PIN, APDU, Origin, trust, cập nhật.
- `Ky-so-da-nang-Windows-x64-Setup.exe` v2026.08.28.5 — bản Windows để đối chiếu.
- **Token VGCA thật** — PC/SC từ .NET 10 qua gói `PCSC`: ATR, EF.DIR, EF.ODF, EF.TokenInfo, EF.AODF, EF.PrKDF,
  EF.CDF, EF 0001. Chỉ lệnh đọc, không gửi `VERIFY`.

Tài liệu tham chiếu:

- [Pkcs11Interop](https://github.com/Pkcs11Interop/Pkcs11Interop) · [gói NuGet 5.3.0](https://www.nuget.org/packages/Pkcs11Interop/)
- [OpenSC macOS Quick Start](https://github.com/OpenSC/OpenSC/wiki/macOS-Quick-Start) ·
  [pkcs15-cert.c](https://github.com/OpenSC/OpenSC/blob/master/src/libopensc/pkcs15-cert.c) — đọc DER trần, không giải nén
- [Smart card integration in macOS Sierra: CryptoTokenKit plugin](https://ludovicrousseau.blogspot.com/2018/09/smart-card-integration-in-macos-sierra.html)
- [MDN — Mixed content](https://developer.mozilla.org/en-US/docs/Web/Security/Mixed_content) ·
  [Apple Developer Forums](https://developer.apple.com/forums/thread/736105) — Safari chặn loopback HTTP
- [Bit4id Manual for Mac OS](https://suport.aoc.cat/en-US/article/?servei=tcat&id=KA-06988_bit4id-manual-for-a-mac-os) — đường dẫn `libbit4xpki.dylib`
- [Avalonia macOS](https://docs.avaloniaui.net/docs/platform-specific-guides/macos) ·
  [notarization](https://docs.avaloniaui.net/accelerate/tools/parcel/apple/notary)
- [NEAC — cấp phép dịch vụ ký số từ xa](https://neac.gov.vn/vi/tin-tuc-su-kien/detail/misa-duoc-cap-phep-cung-cap-dich-vu-chu-ky-so-tu-xa,-khong-can-usb-token-195.htm)
- [Apple's own CCID driver in Sonoma](https://blog.apdu.fr/posts/2023/11/apple-own-ccid-driver-in-sonoma/) ·
  [macOS Sequoia and smart cards status](https://blog.apdu.fr/posts/2024/10/macos-sequoia-and-smart-cards-status/) ·
  [danh sách đầu đọc CCID](https://ccid.apdu.fr/ccid/section.html)
- [Chrome — Local Network Access](https://developer.chrome.com/blog/local-network-access)
- [Rust — nâng phiên bản Apple tối thiểu](https://blog.rust-lang.org/2023/09/25/Increasing-Apple-Version-Requirements/) ·
  [hạ x86_64-apple-darwin xuống Tier 2](https://blog.rust-lang.org/2025/08/19/demoting-x86-64-apple-darwin-to-tier-2-with-host-tools) ·
  [rustc book — *-apple-darwin](https://doc.rust-lang.org/rustc/platform-support/apple-darwin.html)
- [docs.rs — pcsc 2.9.0](https://docs.rs/pcsc/2.9.0/pcsc/); phiên bản crate tra trên crates.io ngày 2026-09-17.

> **Tiếp:** [01-token-vgca.md](01-token-vgca.md) — bên trong token Ban Cơ yếu có gì.
