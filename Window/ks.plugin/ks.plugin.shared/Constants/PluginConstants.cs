using System.Reflection;

namespace ks.plugin.shared.Constants
{
    /// <summary>
    /// Hằng số nhận diện plugin. Cổng và tên đi vào cả trình cài đặt lẫn FE nên phải cố định ở một chỗ.
    /// </summary>
    public static class PluginConstants
    {
        /// <summary>
        /// Cổng lắng nghe trên 127.0.0.1. Chọn cổng cao, ít đụng phần mềm phổ biến; FE dò đúng cổng này để
        /// biết máy đã cài plugin hay chưa.
        /// </summary>
        public const int Port = 17739;

        public const string Ten = "Plugin ký số";

        public const string TaiNguyenBieuTuong = "ky-so.ico";

        public const string TenKhoaMotBanChay = "KySoPlugin.MotBanChay";

        public const string TenTinHieuMoCuaSo = "KySoPlugin.MoCuaSo";

        /// <summary>
        /// Phiên bản plugin, đọc từ &lt;Version&gt; của ks.plugin.api lúc build và trả về ở
        /// api/plugin/trang-thai để BE đối chiếu với whitelist Plugin:PhienBanPhuHop. Đọc từ assembly nên
        /// csproj là nguồn duy nhất; nâng phiên bản thì thêm giá trị mới vào whitelist của MỌI backend đang
        /// dùng plugin, thiếu một backend là bên đó báo plugin lỗi thời ngay sau khi người dùng cập nhật.
        /// </summary>
        public static readonly string PhienBan = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0] ?? string.Empty;

        /// <summary>
        /// Origin của trang web được phép ĐỌC kết quả trả về. Ghim trong mã chứ không chỉ để ở
        /// appsettings.json, vì bản phát hành là MỘT file exe không kèm file cấu hình nào — quên cập nhật
        /// danh sách này thì triệu chứng là "đã cài plugin mà trang web vẫn báo chưa cài", rất tốn công dò.
        ///
        /// Đây KHÔNG phải hàng rào bảo mật: header Origin do phía gọi tự đặt, curl hay mã độc đặt tuỳ ý.
        /// Nó chỉ là điều kiện để trình duyệt cho JavaScript đọc câu trả lời.
        /// </summary>
        public static readonly string[] OriginMacDinh =
        [
            "https://ksts.yna.io.vn",
            "https://v2.nhaplieu.com",
            "http://localhost:4200",
            "https://localhost:4200",
            "http://localhost:3000",
            "https://localhost:3000"
        ];
    }
}
