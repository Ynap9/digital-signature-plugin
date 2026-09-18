# Plugin ký số cho macOS

Bản macOS của plugin ký số, viết bằng Rust. Đang phát triển, chưa có mã nguồn trong thư mục này.

Tài liệu này ghi lại hướng làm và những gì đã biết, để khi bắt tay vào viết không phải dò lại. Hợp đồng API mà
bản này phải thoả nằm ở [README tổng](../README.md) — đọc trước.

## Mục lục

- [Trạng thái](#trạng-thái)
- [Vì sao không port bản Windows sang được](#vì-sao-không-port-bản-windows-sang-được)
- [Hướng làm](#hướng-làm)
- [Dự định theo giai đoạn](#dự-định-theo-giai-đoạn)
- [Vấn đề chưa giải quyết](#vấn-đề-chưa-giải-quyết)

## Trạng thái

Chưa có mã nguồn. Mọi thứ dưới đây là hướng đi, không phải thiết kế đã chốt, trừ một điều đã quyết: **ngôn ngữ
là Rust**.

## Vì sao không port bản Windows sang được

Bản Windows dựa vào một tính chất mà macOS không có. Trên Windows, middleware của hãng token đăng ký một
provider mật mã với hệ điều hành, làm chứng thư trên token tự hiện ra trong kho chứng thư, và ứng dụng đọc được
như chứng thư thường — không cần biết gì về token.

macOS có Keychain, nhưng một driver PKCS#11 không tự đẩy chứng thư trên token vào Keychain theo cách tương
đương. Nghĩa là bản macOS phải **nói trực tiếp với token qua PKCS#11**, thay vì nhờ hệ điều hành làm trung gian.

Đó là một mô hình khác, không phải một biến thể. Toàn bộ phần đọc chứng thư và thực hiện phép ký phải viết lại;
chỉ phần hợp đồng HTTP là giữ nguyên.

## Hướng làm

**Giữ nguyên hợp đồng.** Cổng `127.0.0.1:17739`, chỉ loopback, cùng envelope, cùng bảy route như bản Windows.
Trang web gọi vào không được biết máy người dùng chạy hệ điều hành nào. Đây là ràng buộc cứng.

**Ứng viên thư viện.** Chưa thử nghiệm cái nào, liệt kê để khỏi phải tìm lại:

| Việc | Ứng viên |
|---|---|
| Nói với token | `cryptoki` — binding PKCS#11 được bảo trì tốt nhất trong Rust |
| Máy chủ HTTP | `axum` trên `tokio` |
| Biểu tượng khay | `tray-icon`, hoặc bỏ khay và chạy hẳn dạng tiến trình nền |
| Tự khởi động | LaunchAgent, file plist trong `~/Library/LaunchAgents` |
| Đóng gói | `pkgbuild` và `productbuild` ra `.pkg` |

**Đường tới thư viện PKCS#11.** Middleware của hãng token cài một `.dylib`; plugin nạp file đó lúc chạy. Đường
dẫn khác nhau theo hãng và theo phiên bản nên không ghim cứng được, phải dò theo danh sách đường dẫn đã biết
rồi cho phép khai đè bằng cấu hình.

**Phát hành.** Gatekeeper bắt buộc ký bằng Developer ID và notarize. Đây không phải lựa chọn như trên Windows,
không có đường tránh: binary chưa notarize thì máy người dùng không mở được.

## Dự định theo giai đoạn

Thứ tự này đặt phần rủi ro nhất lên trước, để nếu có chặn thì chặn sớm.

1. **Dò đường tới token.** Một chương trình dòng lệnh nhỏ: nạp `.dylib` của hãng, liệt kê slot, liệt kê chứng
   thư, ký thử một mẩu dữ liệu. Chưa cần HTTP, chưa cần giao diện. Bước này trả lời câu hỏi lớn nhất ở mục
   [Vấn đề chưa giải quyết](#vấn-đề-chưa-giải-quyết).
2. **Dựng máy chủ loopback** với đủ bảy route, trả đúng envelope. Đối chiếu từng route với bản Windows bằng
   cùng một bộ lệnh `curl`.
3. **Phiên ký giữ handle khoá** cho cả lô, để một lô chỉ hỏi PIN một lần.
4. **Chạy nền và tự khởi động** qua LaunchAgent, một bản chạy duy nhất.
5. **Đóng gói `.pkg`**, ký và notarize.

Giai đoạn 1 và 2 độc lập với nhau về mã nguồn nhưng nên làm đúng thứ tự: dựng xong máy chủ mà giai đoạn 1 phát
hiện không ký được thì công sức bỏ đi.

## Vấn đề chưa giải quyết

**Ai hiện hộp nhập PIN.** Đây là câu hỏi quyết định, cần trả lời trước khi viết dòng mã nào.

[README tổng](../README.md) nêu một nguyên tắc: plugin không bao giờ chạm vào mã PIN. Bản Windows thoả được vì
middleware tự hiện hộp PIN và PIN đi thẳng từ bàn phím vào provider mật mã.

Với PKCS#11 nạp trực tiếp thì mặc định **không** như vậy: hàm đăng nhập nhận PIN như một tham số, nghĩa là ứng
dụng phải tự hiện hộp nhập rồi truyền PIN vào — đúng thứ nguyên tắc kia cấm.

Đường thoát là cờ *protected authentication path*: nếu thư viện của hãng khai cờ đó, việc nhập PIN do
middleware hoặc thiết bị đảm nhiệm và ứng dụng đăng nhập mà không truyền PIN. Việc đầu tiên của giai đoạn 1 là
kiểm xem thư viện của hãng có khai cờ này không.

Nếu không có, thì đây là **thay đổi mô hình bảo mật, không phải chi tiết kỹ thuật**, và phải được chấp thuận
trước khi viết mã. Không tự quyết trong lúc code.
