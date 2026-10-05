# Tài liệu plugin ký số — bản Windows

> Chép từ tri thức dự án KSTS (`ksts/.claude/`) ngày 2026-09-25, chỉ lấy phần liên quan tới plugin. Trong
> repo này mã nguồn plugin nằm ở [`../ks.plugin/`](../ks.plugin/README.md) (bên KSTS tên cũ là `ksts.plugin`).

## Đọc theo thứ tự

| File | Nội dung |
|---|---|
| [ky-so-web-vs-desktop.md](ky-so-web-vs-desktop.md) | **Đọc đầu tiên.** Vì sao ký số trên web phải có plugin ở máy người dùng |
| [luong-ky-so-hang-loat.md](luong-ky-so-hang-loat.md) | Luồng ký đang chạy — trang web làm người đưa thư giữa máy chủ và plugin, số đo thật |
| [plugin-ky-so.contract.md](plugin-ky-so.contract.md) | **Hợp đồng API của plugin** — route, envelope, hộp PIN, CORS, bộ cài và phiên bản |
| [ky-so-plugin.plan.md](ky-so-plugin.plan.md) | Kế hoạch plugin: đã làm gì, điểm cần chú ý, việc còn phải làm ngay |
| [bao-mat-agent-ky-so.md](bao-mat-agent-ky-so.md) | 🔬 **Nghiên cứu, tối ưu sau**: threat model, topology B, job ticket, WYSIWYS |

Tài liệu gắn nhãn 🔬 là **nghiên cứu chưa thi công** — đừng đọc chúng như mô tả hệ thống đang chạy.

Bản macOS có bộ tài liệu riêng ở [../../MacOS/docs/](../../MacOS/docs/README.md), phải giữ **nguyên hợp đồng**
ở [plugin-ky-so.contract.md](plugin-ky-so.contract.md).

## Phần nào nói về backend

`luong-ky-so-hang-loat.md` và `ky-so-web-vs-desktop.md` mô tả cả phía máy chủ KSTS (`lo-ky`, `IHangDoiKy`,
`ISigningKey`, `Signing:Nguon`). Những thứ đó **không** nằm trong repo này; giữ lại vì plugin chỉ là một mắt
xích của luồng ký và phải khớp với phía gọi. Hợp đồng lô ký đầy đủ ở `.claude/contracts/lo-ky.contract.md`
bên repo `ksts`.
