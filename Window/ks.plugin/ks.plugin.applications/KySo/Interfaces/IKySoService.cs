using ks.plugin.applications.KySo.Dtos;
using ks.plugin.external.Signing.Dtos;

namespace ks.plugin.applications.KySo.Interfaces
{
    /// <summary>
    /// Ký hộ máy chủ bằng khoá trên token. Máy chủ dựng PDF và SignedAttributes rồi nhờ ký; plugin không cần
    /// biết nội dung file, còn khoá bí mật thì không bao giờ rời khỏi chip.
    /// </summary>
    public interface IKySoService
    {
        /// <summary>Mở phiên ký - chỗ duy nhất hộp PIN bật lên. Trả chứng thư phần công khai cho máy chủ.</summary>
        MoPhienKetQuaDto OpenSession(MoPhienDto input);

        /// <summary>
        /// Ký cả một lô yêu cầu trong MỘT lời gọi. Gom theo lô vì token ký tuần tự: nếu mỗi chữ ký một vòng
        /// đi-về thì với 5000 file, riêng độ trễ đường truyền đã cộng thêm hàng chục phút.
        ///
        /// Một yêu cầu hỏng không làm hỏng cả lô - trả lỗi riêng cho yêu cầu đó rồi ký tiếp.
        /// </summary>
        List<KetQuaKyDto> Sign(KyLoDto input);

        /// <summary>Đóng phiên, giải phóng handle khoá.</summary>
        void CloseSession();
    }
}
