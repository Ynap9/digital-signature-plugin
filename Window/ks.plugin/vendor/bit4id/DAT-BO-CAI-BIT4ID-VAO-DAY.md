# Đặt bộ cài middleware bit4id vào thư mục này

Middleware là phần mềm của **hãng token**, không phải mã nguồn của dự án, nên không nằm sẵn trong repo.
Lấy file cài từ đơn vị cấp chứng thư số (Ban Cơ yếu Chính phủ / nhà cung cấp token) rồi thả vào đây.

```
ks.plugin/vendor/bit4id/
  ├─ bit4id-xpki-x.y.z.exe      ← hoặc .msi, đặt tên gì cũng được
  └─ tham-so.txt                ← tuỳ chọn, xem bên dưới
```

`dong-goi.ps1` tự nhặt **file `.exe` hoặc `.msi` đầu tiên** trong thư mục này và bỏ vào bộ cài. Không có file
nào thì vẫn đóng gói được, chỉ là bộ cài không tự cài được middleware — người dùng phải tự cài trước.

## `tham-so.txt` — cờ chạy ngầm

Trình cài đặt chạy ngầm bằng cờ nào thì tuỳ loại, mà bit4id phát hành khi thì MSI, khi thì InstallShield,
khi thì NSIS. Nếu cờ mặc định không đúng, ghi cờ đúng vào `tham-so.txt` (một dòng):

| Loại bộ cài | Cờ thường dùng |
|---|---|
| MSI | `/qn /norestart` ← mặc định cho `.msi` |
| NSIS | `/S` ← mặc định cho `.exe` |
| InstallShield | `/s /v"/qn"` |
| Inno Setup | `/VERYSILENT /NORESTART` |

**Kiểm tra trước khi phát hành**: chạy tay đúng dòng lệnh đó trên một máy sạch, xác nhận không hiện cửa sổ
nào và cắm token vào thì chứng thư hiện trong `certmgr.msc` → Personal → Certificates.

## Vì sao không tự tải về từ Internet

Đây là phần mềm đụng tới kho khoá mật mã của cả máy. Tải một binary không rõ nguồn rồi cài ngầm với quyền
quản trị là đúng kịch bản mà một cuộc tấn công chuỗi cung ứng cần. Chỉ dùng file lấy trực tiếp từ đơn vị
cấp chứng thư, và nếu họ có công bố mã băm thì đối chiếu trước khi bỏ vào đây.
