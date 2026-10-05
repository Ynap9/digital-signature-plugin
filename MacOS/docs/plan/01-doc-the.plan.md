# Bước 1 — Đọc thẻ chỉ-đọc bằng `ks-probe`

> **Bước 1/9** · trước: [00-chuan-bi.plan.md](00-chuan-bi.plan.md) · mục lục: [README.md](README.md) ·
> Trạng thái: ✅ **xong 2026-09-25**, đã chạy trên token thật

## Input

- Token VGCA đang cắm trên máy dev Windows (đầu đọc `bit4id TokenME EVO v2`).
- Bảng hằng số đã đo bằng .NET: [../01-token-vgca.md](../01-token-vgca.md).
- Kiến trúc card layer: [../architecture/03-card-layer.md](../architecture/03-card-layer.md).

## Steps

1. `card::transport::PcscTransport` trên crate `pcsc`: mở context, liệt kê đầu đọc, kết nối `SHARE_SHARED`, đọc ATR.
2. `card::apdu`: dựng APDU ngắn/mở rộng, xử lý `61xx` (GET RESPONSE) và `6Cxx`, ánh xạ `SW` sang `CardError`.
   **Danh sách INS cho phép** ở bước này chỉ có `SELECT (A4)`, `READ BINARY (B0)`, `GET RESPONSE (C0)`.
3. `card::pkcs15`: parse TLV/BER cho EF.DIR, ODF, TokenInfo, AODF, PrKDF, PuKDF, CDF — hàm thuần, nhận `&[u8]`.
4. Dump byte thật của từng EF vào `crates/ks-plugin-external/tests/fixtures/vgca/` (đặt cạnh crate parse để test
   dùng `include_bytes!`; plan gốc ghi `tools/ks-probe/fixtures/`) (dữ liệu công khai, không có PIN hay khoá bí mật) và
   viết test đơn vị parse từ fixture — chạy được không cần token.
5. `tools/ks-probe`: lệnh `readers`, `atr`, `dump` in ra bảng giống bảng ở `01-token-vgca.md`. Lệnh dò thẻ `send` và
   tuỳ chọn `dump --save` chỉ dùng một lần để dò cấu trúc và sinh fixture — đã xoá sau khi xong.
6. Đối chiếu từng giá trị với số đo .NET: AID, AODF (PIN ref `0x03`, 4–16, pad `0xFF`, PUK ref `0x06`), PrKDF (key ref
   `0x10`, RSA 3072, ID `BE72ECF5`, path `DF10`), CDF (ID khớp, EF `0001`).
7. Chốt: `cargo test --workspace` + `cargo clippy -- -D warnings` sạch.

## Expected output

```text
ks-probe dump
Reader : bit4id TokenME EVO v2
ATR    : 3B FF 18 00 … 42 34 44 …   ("B4D") → Bit4idVgcaDriver
PIN    : ref 0x03, len 4..16, pad FF, UTF-8      PUK ref 0x06
Key    : ref 0x10, RSA 3072, id BE72ECF5, needs PIN
Cert   : id BE72ECF5 → EF 0001, tag 7A, 1482 bytes (compressed)
```

## Điểm cần chú ý

- ⚠️ **Không gửi `VERIFY` ở bước này**, kể cả dạng hỏi lượt thử. Công cụ không có lệnh nào nhận PIN.
- Máy dev đang cài middleware bit4id: nó có thể giữ thẻ. Kết nối `SHARE_SHARED` + `SCardBeginTransaction` quanh
  mỗi chuỗi lệnh, đừng giành `EXCLUSIVE` ở bước đọc.
- ~~Không ghim cứng AID nếu EF.DIR đọc được~~ — đo thật: EF.DIR chỉ đọc được **sau khi** chọn applet, nên AID phải
  ghim; EF.DIR chỉ để xác nhận (xem ⚠️ ở [../architecture/03-card-layer.md](../architecture/03-card-layer.md)).
- Fixture là tài sản test lâu dài: bước 5 chạy lại đúng bộ test này trên Mac.

> **Tiếp:** [02-chung-thu.plan.md](02-chung-thu.plan.md) — lấy chứng thư DER từ phần nén `7A`.
