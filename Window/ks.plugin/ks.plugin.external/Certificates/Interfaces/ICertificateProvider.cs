using ks.plugin.external.Certificates.Dtos;

namespace ks.plugin.external.Certificates.Interfaces
{
    /// <summary>
    /// Nguồn chứng thư số trên MÁY NGƯỜI DÙNG - đúng chỗ phải đọc, khác với BE vốn chỉ đọc được store của
    /// máy chạy API. Xem .claude/docs/ky-so-web-vs-desktop.md.
    /// </summary>
    public interface ICertificateProvider
    {
        /// <summary>
        /// Liệt kê MỌI chứng thư đọc được, kèm lý do vì sao một cert không ký được; việc lọc do tầng service
        /// quyết định. Danh sách rỗng mà không rõ nguyên nhân là kiểu lỗi tốn cả buổi để chẩn đoán, nên store
        /// nào mở lỗi thì ghi vào <see cref="CertScanResultDto.StoreDiagnostics"/> chứ không ném.
        ///
        /// Chỉ đọc METADATA của khoá, KHÔNG dùng khoá để ký nên KHÔNG bật hộp thoại nhập PIN.
        ///
        /// KHÔNG kết luận chứng thư có thuộc CA tin cậy hay không: plugin chạy trên máy không kiểm soát được,
        /// cờ tin cậy do plugin gửi lên là vô giá trị. Việc thẩm định chuỗi là của BE.
        /// </summary>
        CertScanResultDto GetCertificates();
    }
}
