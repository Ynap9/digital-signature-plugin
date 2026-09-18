using ks.plugin.applications.Plugin.Dtos;

namespace ks.plugin.applications.Plugin.Interfaces
{
    /// <summary>
    /// Danh tính plugin. FE gọi để biết máy đã cài plugin hay chưa - gọi không tới hoặc lỗi nghĩa là chưa cài.
    /// </summary>
    public interface IPluginService
    {
        /// <summary>
        /// Tên và phiên bản plugin đang chạy. KHÔNG chạm tới chứng thư hay token: đây là phép dò, phải trả
        /// lời tức thì và không bao giờ bật hộp thoại nào lên màn hình người dùng.
        /// </summary>
        ViewTrangThaiDto GetTrangThai();
    }
}
