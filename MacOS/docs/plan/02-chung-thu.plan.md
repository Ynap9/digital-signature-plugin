# Bước 2 — Lấy chứng thư DER

> **Bước 2/9** · trước: [01-doc-the.plan.md](01-doc-the.plan.md) · mục lục: [README.md](README.md) ·
> Trạng thái: ✅ **xong 2026-09-25 bằng đường `.cer`** — giải nén `7A` dừng giữa chừng theo chỉ đạo "bê chứng thư
> từ Windows sang", xem mục Kết quả cuối file

Không có DER thì không có `thumbprint`, không có `chungThuBase64` cho `mo-phien`, không dựng được `SignCertDto`.
Đây là ẩn số lớn thứ hai sau PIN.

## Input

- Bản nén: EF `0001`, tag `7A`, 1482 byte (fixture từ bước 1).
- Bản gốc: DER 1787 byte của **cùng** chứng thư — xuất từ Windows cert store (bản plugin C# đang đọc được), serial
  `30D8C57549BFA5DC`.
- Đã biết: `deflate`/`zlib`/`gzip` ở mọi offset đều trượt; OpenSC `pkcs15-cert.c` không giải nén.

## Steps

1. **Dò thuật toán bằng bản rõ đã biết** trong `tools/ks-probe` (lệnh `crack-cert`), theo thứ tự rẻ → đắt:
   1. bóc header TLV `7A` cho đúng (độ dài, byte cờ/thuật toán nếu có) trước khi thử giải nén;
   2. raw deflate với **preset dictionary** — thử từ điển là các chuỗi DER hay gặp (OID, tên CA Ban Cơ yếu, phần
      đầu chứng thư CA trung gian `cpg2`/`rootcag2`);
   3. các định dạng nén hay gặp trên thẻ (zlib có dictionary id, LZ-family) — đối chiếu tài liệu bit4id/OpenSC;
2. Giới hạn thời gian **hai ngày công**. Hết hạn chưa ra ⇒ chuyển sang bước 3 của mục này, không đào tiếp.
3. **Đường dự phòng** (làm luôn, kể cả khi dò ra — để còn đường lùi):
   - Đọc khoá công khai trên thẻ từ PuKDF (`7004`) → modulus + exponent.
   - Người dùng nạp `.cer` một lần; plugin so khoá công khai trong file với khoá trên thẻ; lệch ⇒ từ chối.
   - Lưu `.cer` vào `~/Library/Application Support/KySoPlugin/certs/<keyId>.cer` (dữ liệu công khai).
4. Từ DER: `thumbprint` (SHA-1, hex HOA), CN, subject, issuer, serial, hạn, KeyUsage; `reason` theo **đúng thứ tự**
   của `CertificateProvider.cs`.
5. Test: `thumbprint` tính bằng Rust **trùng từng ký tự** với `thumbprint` plugin C# trả về cho cùng token.
6. Chốt: `cargo test --workspace` sạch.

## Expected output

- `ks-probe cert --out cert.der` ra đúng 1787 byte, SHA-256 trùng bản xuất từ Windows; **hoặc** đường `.cer` chạy
  và từ chối được một `.cer` khác khoá.
- `SignCertDto` dựng từ thẻ trùng bản C# ở mọi trường trừ `keyProvider` và `source` (Mac luôn `2`).

## Điểm cần chú ý

- Đường `.cer` thêm một thao tác cho người dùng và một màn hình ở bước 7 — nếu phải đi đường này, báo lại để cân
  nhắc UX trước khi làm bước 7.
- Không ghi đè: nếu dò ra thuật toán, đường `.cer` vẫn giữ làm dự phòng cho thẻ đời khác.
- Đây vẫn là bước **chỉ đọc**, không có lệnh nào nhận PIN.

## Kết quả (2026-09-25)

- **Nén `7A` = deflate thô + preset dictionary.** Giải được đúng 8 byte đầu DER rồi báo "distance too far back".
  Tách token deflate: 58/85 tham chiếu trỏ vào từ điển, sâu tới 26 283 byte; 40/58 khớp các CA Ban Cơ yếu
  (`rootca`, `cp`, `rootcag2`, `cpg2`, `dcscag2`), phần còn lại là chứng thư khác (có cả ngày hiệu lực) ⇒ từ điển là
  chuỗi nhiều chứng thư ghép lại. **Không** thấy ở dạng thô hay nén zlib trong `C:\Windows\System32\bit4*.dll`.
  Dừng ở đây theo chỉ đạo, chưa tới hạn hai ngày.
- **Đường `.cer` chạy**: khoá công khai đọc từ `DF10` (đường dẫn lấy ở PuKDF `7004`, không PIN), `.cer`/PEM của khoá
  khác bị từ chối, đúng khoá thì lưu vào `CertificateStore` theo key ID. Đo trên token thật: Subject, Issuer, Serial
  `30D8C57549BFA5DC`, thumbprint `68DBABDD…C887` **trùng từng ký tự** với .NET `X509Certificate2`.
- `SignCertDto` dựng ở `certificate::sign_cert`; chỉ còn hai nhánh `reason` (hết hạn → KeyUsage) vì bản Mac chỉ
  liệt kê chứng thư gắn khoá trên thẻ — nhánh "không có khoá bí mật" và "khoá phần mềm" không thể xảy ra.
- Test dùng hai CA **công khai** (`cpg2`, `rootcag2`) với giá trị kỳ vọng lấy từ .NET. Chứng thư cá nhân dùng để đo
  **không** đưa vào repo.
- ⚠️ Hệ quả UX cho bước 7: người dùng phải nạp `.cer` một lần cho mỗi token (xuất từ máy Windows đang cài
  middleware, hoặc lấy từ nơi cấp chứng thư). Cần một mục menu "Nạp chứng thư…" và thông báo rõ khi chưa nạp.

> **Tiếp:** [03-ky-that.plan.md](03-ky-that.plan.md) — `VERIFY` và ký thật.
