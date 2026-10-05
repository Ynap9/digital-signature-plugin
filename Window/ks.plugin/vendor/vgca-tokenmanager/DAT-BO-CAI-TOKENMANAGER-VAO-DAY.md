# Đặt bộ cài TokenManager vào thư mục này

> Trình điều khiển cho **token VGCA đời cũ (trước 2022)**, cạnh [`../bit4id/`](../bit4id/DAT-BO-CAI-BIT4ID-VAO-DAY.md)
> cho token đời mới. Cập nhật 2026-10-05.

TokenManager là phần mềm của **Ban Cơ yếu Chính phủ**, không nằm sẵn trong repo. Tải ở
`https://dichvucong.ca.gov.vn/#/page/taitainguyen/PM` → *Trình điều khiển thiết bị - TokenManager* (bản 64-bit),
giải nén rồi thả file exe vào đây.

```text
ks.plugin/vendor/vgca-tokenmanager/
  └─ VGCAv1_Installer_1.0.21.0402.exe    ← đặt tên gì cũng được
```

`dong-goi.ps1` nhặt **file `.exe` hoặc `.msi` đầu tiên** trong thư mục này và bỏ vào bộ cài qua define
`TOKEN_MANAGER_SETUP`; `.msi` chạy bằng `msiexec /qn /norestart`, `.exe` bằng `/S`. Cờ khác thì ghi vào
`tham-so.txt` cạnh file cài — cùng quy tắc với [`../bit4id/`](../bit4id/DAT-BO-CAI-BIT4ID-VAO-DAY.md). Thư mục
này đã có sẵn `tham-so.txt` ghi `/S`.

## Bản đang dùng

| Mục | Giá trị |
|---|---|
| Tên sản phẩm | `VGCA Token Manager` 1.0.21.0402 |
| Ký mã | `CN=BAN CƠ YẾU CHÍNH PHỦ` — Authenticode hợp lệ |
| SHA-256 file zip tải về | `403D14703608590968E7F85B53D3EF7A0F06E5E9D2F89490BF996A2B90DADE08` |
| Kiểu bộ cài | NSIS bọc ngoài ⇒ chạy ngầm bằng `/S` |
| Cài vào máy | `VGCA Client 8.3` (SafeNet) |
| Provider đăng ký | CSP `eToken Base Cryptographic Provider`, KSP `SafeNet Smart Card Key Storage Provider` |

Bộ cài nhận ra máy đã có TokenManager khi có provider bắt đầu bằng `eToken` trong
`HKLM\SOFTWARE\Microsoft\Cryptography\Defaults\Provider`, hoặc có `System32\eTOKCSP.dll`.

⚠️ **Chưa chạy thử `/S` trên máy sạch.** Vỏ ngoài là NSIS nên `/S` tắt được wizard của nó, nhưng chưa xác nhận
nó có truyền chế độ ngầm xuống bộ cài SafeNet bên trong hay không. Nếu bên trong vẫn bật cửa sổ thì bộ cài
plugin sẽ đứng chờ — thử trên máy sạch trước khi phát hành, kiểm `certutil -csplist` có `eToken Base
Cryptographic Provider`.

Không tự tải về từ Internet trong lúc cài, cùng lý do ở [`../bit4id/`](../bit4id/DAT-BO-CAI-BIT4ID-VAO-DAY.md):
chỉ dùng file lấy trực tiếp từ Ban Cơ yếu, kiểm chữ ký Authenticode trước khi bỏ vào đây.
