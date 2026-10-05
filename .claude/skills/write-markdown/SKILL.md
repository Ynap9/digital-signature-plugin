---
name: write-markdown
description: Quy ước viết và sửa mọi file Markdown (.md) của repo Plugin ký số — tài liệu trong Window/docs và MacOS/docs, plan, architecture, contract, README, memory. Đọc TRƯỚC khi tạo hoặc sửa một file .md để nó khớp ngôn ngữ, bố cục, độ dài và cách bảo trì của dự án. Áp dụng cho phiên chính và cho mọi agent được sinh ra.
---

# Skill: Viết Markdown (Plugin ký số)

Đọc trước khi tạo hoặc sửa bất kỳ `.md` nào. Mục tiêu: mọi tài liệu đọc như **một hệ thống** — cùng ngôn
ngữ, cùng bố cục, cùng cách cập nhật.

## Ngôn ngữ

- **Toàn bộ tài liệu viết TIẾNG VIỆT** — nghiệp vụ là tiếng Việt, thuật ngữ dịch sang tiếng Anh chỉ tạo thêm
  một lớp phải đối chiếu.
- Giữ nguyên tên định danh trong code: `ISigningSession`, `KySoConstants`, `OriginMacDinh`, tên route, tên
  crate, tên hằng số.
- Sửa file đã có thì **theo ngôn ngữ của file đó**, không đổi giữa dòng.
- ⚠️ **Tài liệu tiếng Việt, comment trong code tiếng Anh** — hai luật khác nhau, đừng lẫn. Chú thích `//` bên
  trong khối code của tài liệu là **lời giảng cho người đọc** nên vẫn tiếng Việt; chỉ comment nằm thật trong
  repo mới là tiếng Anh.

## Độ dài — giữ file nhỏ

| Loại file | Trần |
|---|---|
| `architecture/`, `plan/` | **100 dòng** |
| `docs/`, `*.contract.md` | **180 dòng** — một chủ đề trọn vẹn được phép dài hơn |

Vượt trần thì xử theo mức vượt:

| Vượt | Việc phải làm |
|---|---|
| **≤ 10 dòng** | **KHÔNG tách.** Nén lại: siết câu, gộp gạch đầu dòng hoặc dòng bảng gần nhau. Tách file vì mấy dòng chỉ làm vụn một chủ đề. |
| **> 10 dòng** | **TÁCH thành nhiều file**: một `README.md` làm mục lục + các file đánh số. |

Một chủ đề một file; liên kết file liên quan bằng đường dẫn tương đối.

### Tách thì các phần PHẢI còn nối với nhau

Người đọc (hoặc agent) mở đúng một phần phải biết còn phần khác và đọc theo thứ tự nào. Mỗi bộ file tách ra
cần **cả hai**:

1. **Mục lục** — `README.md` liệt kê các phần theo thứ tự đọc, mỗi phần một dòng nêu nội dung.
2. **Con trỏ tiếp nối ở cuối mỗi phần**, kèm dòng quay lại ở đầu:

```markdown
> **Phần 2/5** · trước: [01-ten-phan-truoc.md] · mục lục: [README.md]      ← đặt ngay dưới H1
> **Tiếp:** [03-ten-phan-sau.md] — một câu nêu nội dung phần sau.          ← dòng cuối file
```

Đừng để lại một phần đọc như thể nó là cả tài liệu — đó là cách một agent thi công nửa bản kế hoạch.

## Bố cục

- Đúng **một** `#` H1; ngay dưới là một dòng nêu mục đích (`>` blockquote với tài liệu gốc, kèm ngày cập nhật).
- `##` H2 cho từng mục. Dùng **bảng** cho đặc tả trường / bản đồ / danh sách endpoint.
- Khối code **luôn** khai ngôn ngữ (```csharp / ```rust / ```toml / ```jsonc / ```bash / ```text).
- GitHub-flavored Markdown; liên kết nội bộ luôn là **đường dẫn tương đối** tới file `.md`.
- Ghi **vì sao** chứ không thuật lại code. Con số phải nói rõ **đo ở đâu ra**.

## Khuôn từng loại tài liệu

**Plan** — `<Nền tảng>/docs/plan/<buoc>.plan.md` hoặc `*.plan.md`, ≤100 dòng:

- **Trạng thái** ở đầu file (`✅ đã thi công` / `🔶 làm dở` / `📝 chờ duyệt` / `🔬 nghiên cứu`) + trỏ sang
  contract đang chạy.
- **Input** — cái đã có, điều kiện tiên quyết.
- **Steps** — theo thứ tự, mỗi bước tự kiểm được; bước cuối là `dotnet build` (Windows) hoặc `cargo build`
  (macOS).
- **Expected output** — kết quả cụ thể muốn có. **Điểm cần chú ý** — mỗi đánh đổi không hiển nhiên một dòng.

**Architecture** — `<Nền tảng>/docs/architecture/`: README mục lục + file đánh số, mỗi file một tầng/crate,
trách nhiệm và chiều phụ thuộc.

**Contract** — `*.contract.md`: bảng route, thứ tự gọi bắt buộc, ví dụ payload `jsonc`, bảng mã lỗi, mục "bên
gọi phải nắm". Bản Windows là nguồn chân lý; bản macOS phải thoả **nguyên** hợp đồng đó.

**Tài liệu gốc** — `docs/`: quyết định đã chốt + vì sao, số đo thật kèm điều kiện đo, giới hạn đã biết.
Phần **chưa thi công** gom xuống cuối file dưới nhãn 🔬, không trộn vào phần đang chạy.

## Ghi lại bẫy đã sập

**Không có thư mục bug log riêng**: bẫy ghi thành một dòng `⚠️` **ngay trong tài liệu sở hữu chỗ đó**, cạnh
phần mô tả cơ chế — đọc tới cơ chế là thấy luôn bẫy. Khuôn: hiện tượng → nguyên nhân gốc → cách tránh, và nêu rõ
**đừng "sửa" theo hướng nào**.

```markdown
⚠️ Bẫy đã sập một lần: .NET gắn commit SHA vào `InformationalVersion` (`1.0.0+0e8060f9…`) nên hằng số phiên
bản phải cắt ở dấu `+`; bỏ bước cắt là mọi plugin bị đánh là lỗi thời.
```

Bẫy lớn tới mức cần cả một mục thì mở `##` riêng trong đúng file đó, đừng mở file mới.

## Bảo trì

- Code đổi lệch khỏi thứ một tài liệu đang mô tả ⇒ **sửa tài liệu đó trong CÙNG task**, không hẹn lần sau.
- Xong một mốc thì cập nhật trạng thái trong plan tương ứng và bảng "Nền tảng hỗ trợ" ở `README.md` gốc.
- Memory dự án: một fact một file, thêm **đúng một dòng** trỏ vào `MEMORY.md`, không nhồi nội dung vào index.

## Trước khi lưu — checklist

1. Tiếng Việt (hoặc theo đúng file đang sửa)? Trong trần dòng — vượt ≤10 thì nén, vượt >10 thì tách kèm mục lục
   và con trỏ tiếp/trước?
2. Một H1, các H2 rõ, khối code có khai ngôn ngữ, link tương đối còn đúng?
3. Plan có Input · Steps (bước build cuối) · Expected output · Điểm cần chú ý?
4. Tài liệu nào khác đang mô tả cùng chỗ này cần sửa theo? Trạng thái trong plan đã cập nhật?
5. Phát hiện bẫy nào thì đã ghi `⚠️` vào đúng tài liệu sở hữu chỗ đó chưa?
