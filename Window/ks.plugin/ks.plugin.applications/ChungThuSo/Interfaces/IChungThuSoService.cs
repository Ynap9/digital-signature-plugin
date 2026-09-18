using ks.plugin.external.Certificates.Dtos;

namespace ks.plugin.applications.ChungThuSo.Interfaces
{
    /// <summary>
    /// Chứng thư số đọc từ máy người dùng, phục vụ màn chọn chứng thư trước khi ký.
    /// </summary>
    public interface IChungThuSoService
    {
        /// <summary>
        /// Danh sách chứng thư kèm chẩn đoán store. Mặc định trả HẾT, gồm cả cert không ký được: ẩn đi thì
        /// người dùng cắm token rồi vẫn không hiểu vì sao không thấy cert của mình.
        ///
        /// Không cache: token có thể vừa được cắm hoặc vừa rút, đọc lại mỗi lần gọi.
        /// </summary>
        CertScanResultDto GetList(bool onlySignable);

        /// <summary>
        /// Kiểm tra chứng thư đã chọn bằng cách ký thử - đây là chỗ hộp thoại PIN của token bật lên. Bước
        /// liệt kê ở trên chỉ đọc metadata nên không bao giờ hỏi PIN.
        /// </summary>
        TokenVerifyDto KiemTraToken(string thumbprint);
    }
}
