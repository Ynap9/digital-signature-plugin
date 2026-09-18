using ks.plugin.external.Certificates.Dtos;

namespace ks.plugin.external.Certificates.Interfaces
{
    /// <summary>
    /// Kiểm tra một chứng thư có thực sự ký được ngay lúc này hay không.
    /// </summary>
    public interface ITokenVerifier
    {
        /// <summary>
        /// Ký thử một mẩu dữ liệu ngẫu nhiên bằng khoá bí mật của chứng thư. Đây là bằng chứng DUY NHẤT rằng
        /// token đang cắm thật và PIN dùng được - mọi phép đọc metadata đều có thể "đạt hết" trong khi token
        /// đã rút từ lâu. Cũng chính là chỗ hộp thoại PIN của middleware bật lên.
        ///
        /// PIN đi thẳng từ bàn phím vào middleware, KHÔNG đi qua tiến trình này. Kết quả trả về đúng một cờ
        /// boolean kèm lý do hiển thị được, không kèm số lần thử còn lại.
        /// Xem .claude/docs/bao-mat-agent-ky-so.md §4.
        /// </summary>
        TokenVerifyDto Verify(string thumbprint);
    }
}
