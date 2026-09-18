using ks.plugin.external.Signing.Dtos;

namespace ks.plugin.external.Signing.Interfaces
{
    /// <summary>
    /// Phiên ký của một lô: mở khoá trên token một lần rồi giữ handle để ký nhiều lượt.
    ///
    /// Giữ handle KHÁC với cache PIN - đây là thứ khiến cả lô chỉ hỏi PIN đúng một lần, còn PIN vẫn đi thẳng
    /// từ bàn phím vào middleware, không byte nào đi qua tiến trình này.
    /// </summary>
    public interface ISigningSession
    {
        /// <summary>
        /// Mở phiên trên chứng thư đã chọn. Đây là chỗ DUY NHẤT hộp PIN bật lên, vì mở khoá bắt buộc chạm vào
        /// khoá bí mật. Trả về chứng thư phần CÔNG KHAI để máy chủ dựng chuỗi tin cậy và lắp CMS.
        /// </summary>
        MoPhienKetQuaDto MoPhien(string thumbprint);

        /// <summary>
        /// Ký một dãy byte bằng handle đã mở, không hỏi PIN lại. Dãy byte là SignedAttributes do máy chủ dựng
        /// - plugin không cần biết nội dung file vẫn ký được.
        /// </summary>
        byte[] Ky(byte[] duLieu);

        /// <summary>Đóng phiên và giải phóng handle khoá, gọi tường minh khi lô xong hoặc người dùng huỷ.</summary>
        void DongPhien();

        /// <summary>
        /// Đo thời gian một lượt ký thật trên token đang cắm, kèm kích thước khoá và tên provider.
        ///
        /// Cần con số này trước khi bàn tối ưu: token ký TUẦN TỰ nên thời gian một lượt nhân thẳng với số
        /// file, và nó là sàn cứng mà không dòng code nào phá được. Khoá 4096 bit trên thẻ đời cũ tốn hơn
        /// một giây là chuyện bình thường, còn 2048 bit thường quanh 200-300 ms.
        /// </summary>
        DoTocDoKetQuaDto DoTocDo(string thumbprint, int soLan);
    }
}
