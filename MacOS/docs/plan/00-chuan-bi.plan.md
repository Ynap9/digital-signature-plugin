# Bước 0 — Chuẩn bị môi trường và workspace rỗng

> **Bước 0/9** · mục lục: [README.md](README.md) · Trạng thái: 🔶 **xong local 2026-09-25** — chờ workflow CI
> macOS chạy xanh lần đầu (cần push lên GitHub)

## Input

- Máy dev Windows: `rustup` + MSVC đã có (kiểm lại 2026-09-25: Rust 1.98.1, VS 2022 Build Tools).
- Kiến trúc đã duyệt: [../architecture/01-workspace.md](../architecture/01-workspace.md).

## Steps

1. Cài `rustup` (toolchain stable, bản MSVC), thêm `clippy`, `rustfmt`. Ghi phiên bản `rustc` vào
   `rust-toolchain.toml` để mọi máy build cùng một bản.
2. Tạo `MacOS/ks-plugin/` đúng cây thư mục ở kiến trúc: `Cargo.toml` workspace, năm crate rỗng (`ks-plugin-shared`,
   `ks-plugin-external`, `ks-plugin-applications`, `ks-plugin-api`, `app`) và `tools/ks-probe`.
3. Khai `[workspace.dependencies]` với đúng bảng crate ở
   [../architecture/06-conventions.md](../architecture/06-conventions.md); crate con chỉ `x.workspace = true`.
4. Khai chiều phụ thuộc giữa các crate; thử cố ý import ngược (`external` → `api`) để thấy trình biên dịch chặn.
5. `ks-plugin-shared`: `constants` (`PORT`, `DEFAULT_ORIGINS` chép đúng `PluginConstants.OriginMacDinh`,
   `SESSION_IDLE_TIMEOUT_MINUTES`, `PREFLIGHT_TEST_DATA_SIZE = 32`), `response::ApiResponse<T>`.
6. Thêm `.gitignore` cho `target/`.
7. Workflow `.github/workflows/macos.yml` trên `macos-15` (arm64): `cargo fmt --check`, `clippy -D warnings`,
   `cargo test`, `cargo build --release` cho `aarch64-apple-darwin` và `x86_64-apple-darwin` cho mọi push/PR đụng `MacOS/**`. Chưa đóng `.app`
   ở bước này — chỉ cần chứng minh workspace biên dịch được cho Mac.
8. Chốt: `cargo fmt --check`, `cargo clippy --workspace -- -D warnings`, `cargo build` sạch trên Windows **và** workflow
   macOS xanh.

## Expected output

- Workspace build sạch, chưa có logic.
- Test đơn vị: `ApiResponse::ok(json!({}))` serialize ra đúng
  `{"status":1,"data":{},"code":200,"message":"Ok"}`; bản lỗi ra `status: 0`, `code: 500`, `data: null`.

## Điểm cần chú ý

- `DEFAULT_ORIGINS` là **bản sao** danh sách C#; hai chỗ phải đổi cùng lúc. Ghi ⚠️ vào contract khi thi công.
- Không build được cho Mac trên Windows (thiếu SDK Apple, `cargo-zigbuild` không chạy trên host Windows) — CI là
  nơi duy nhất biên dịch phần `cfg(target_os = "macos")`.
- Không thêm crate ngoài bảng khi chưa hỏi; mỗi crate thêm là một mắt xích chuỗi cung ứng trong tiến trình chạm
  token.

> **Tiếp:** [01-doc-the.plan.md](01-doc-the.plan.md) — đọc thẻ chỉ-đọc.
