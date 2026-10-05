---
name: task-workflow
description: Cách chạy một việc nhiều bước trong repo Plugin ký số — nạp tri thức dự án trước, gom TẤT CẢ câu hỏi vào MỘT lượt, giữ một task list hiện rõ và tick từng việc khi xong, chốt bằng build sạch rồi cập nhật tài liệu. Áp dụng cho phiên chính và cho mọi agent được sinh ra. Dùng ở đầu mọi việc không tầm thường.
---

# Skill: Khuôn chạy việc (Plugin ký số)

Áp dụng cho phiên chính và **mọi** agent được sinh ra. Bốn pha: nạp tri thức → hỏi một lượt → theo dõi hiện rõ
→ chốt.

## Pha 0 — Nạp tri thức trước khi động vào code

Tri thức dự án nằm ở các thư mục `docs/` (tiếng Việt), **không** suy ra từ code:

1. [`README.md`](../../../README.md) ở gốc repo — bối cảnh, nền tảng hỗ trợ, hợp đồng API chung.
2. Tài liệu của đúng phần định sửa:
   - bản Windows ⇒ [`Window/docs/`](../../../Window/docs/README.md), hợp đồng ở
     `Window/docs/plugin-ky-so.contract.md`, việc còn dở ở `Window/docs/ky-so-plugin.plan.md`;
   - bản macOS ⇒ [`MacOS/docs/`](../../../MacOS/docs/README.md) — khảo sát, kiến trúc Rust, kế hoạch từng bước.

Tài liệu gắn nhãn 🔬 là **nghiên cứu chưa thi công** (`Window/docs/bao-mat-agent-ky-so.md`, bộ khảo sát macOS) —
đừng đọc nó như mô tả hệ thống đang chạy, và đừng thi công theo nó khi chưa được yêu cầu.

## Pha 1 — Hỏi đúng một lượt

- Trước khi bắt tay, gom **TẤT CẢ** câu hỏi còn treo rồi hỏi **cùng một lượt** (một lời gọi `AskUserQuestion`,
  tối đa 4 câu).
- **Không** nhỏ giọt mỗi lượt một câu.
- Chỉ hỏi thứ thực sự làm đổi việc mình sẽ làm. Cái nào có mặc định hợp lý thì **tự chọn, nói ra, rồi đi tiếp**.
- Không có gì chặn thì bỏ qua pha này, vào Pha 2 luôn.

Ba thứ **phải hỏi**, không được tự quyết: đổi hợp đồng API của plugin (route, envelope, cổng `17739`), đổi hằng
số ký số (`KySoConstants`, `PluginConstants`), và thi công phần 🔬 nghiên cứu.

## Pha 2 — Theo dõi việc hiện rõ

- Chẻ việc thành các bước cụ thể và tạo **task list hiện rõ** (`TaskCreate`) để người dùng thấy kế hoạch.
- Đúng **một** việc `in_progress` mỗi lúc; đặt `in_progress` khi bắt đầu (`TaskUpdate`), đặt `completed` ngay
  khi xong.
- Giữ danh sách đúng thực tế — thêm bước phát hiện giữa đường, xoá bước đã vô nghĩa.
- Tick **khi làm xong từng việc**, không dồn tick một lượt ở cuối.

## Pha 3 — Chốt công việc

| Sửa gì | Chốt bằng |
|---|---|
| Bản Windows | `dotnet build Window/ks.plugin/ks.plugin.sln` sạch |
| Bản macOS (Rust) | `cargo build` + `cargo clippy` sạch trong workspace của `MacOS/` |

Rồi trong **cùng task đó**:

1. Sửa tài liệu nào vừa lệch khỏi code — xem [../write-markdown/SKILL.md](../write-markdown/SKILL.md).
2. Cập nhật trạng thái trong plan của phần vừa làm nếu trạng thái hoặc việc kế tiếp đổi.
3. Nói thẳng phần nào **chưa** làm được và vì sao. Báo xong khi chưa xong là kiểu lỗi tốn nhất ở dự án này.

## Gặp bẫy thì ghi lại

Bẫy phát hiện trong lúc làm hoặc lúc review được ghi thành một dòng `⚠️` **ngay trong tài liệu sở hữu chỗ đó**
(không có thư mục bug log riêng): hiện tượng → nguyên nhân gốc → cách tránh. Khuôn ở
[../write-markdown/SKILL.md](../write-markdown/SKILL.md). Ghi rồi mới sửa, hoặc thêm một việc vào task list cho
phần sửa.

## Khi giao việc cho agent

Nhét vào prompt của agent yêu cầu chạy theo **đúng khuôn này**:

1. đọc `README.md` gốc + `docs/` của phần liên quan trước khi sửa gì,
2. gom mọi câu hỏi trả về **một lượt**, không nhỏ giọt,
3. giữ và báo lại **checklist** việc con, tick từng cái,
4. **ghi bẫy** vào đúng tài liệu sở hữu chỗ đó,
5. chốt bằng build sạch của đúng phần mình sửa.

## Checklist nhanh

1. Đã đọc `README.md` gốc + tài liệu của phần định sửa?
2. Đã gom và hỏi hết câu hỏi trong một lượt (hoặc xác nhận không cần hỏi)?
3. Có task list hiện rõ, đúng một việc `in_progress`, tick khi xong từng việc?
4. Build sạch, tài liệu và plan đã cập nhật cùng task?
5. Gặp bẫy nào đã ghi `⚠️` vào đúng tài liệu chưa?
6. Nếu có giao việc cho agent, prompt đã mang năm luật trên chưa?
