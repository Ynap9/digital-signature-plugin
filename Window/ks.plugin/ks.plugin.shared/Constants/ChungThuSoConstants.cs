namespace ks.plugin.shared.Constants
{
    /// <summary>
    /// Dấu hiệu nhận biết khoá nằm trên phần cứng hay trong phần mềm, dò theo TÊN provider của khoá.
    /// Giữ cùng bộ giá trị với BE để hai bên phân loại nguồn chứng thư giống nhau.
    /// </summary>
    public static class ChungThuSoConstants
    {
        /// <summary>
        /// Cỡ mẩu dữ liệu ngẫu nhiên đem ký thử để kiểm tra token. Chỉ cần đủ để middleware thực sự dùng
        /// khoá - đây là lúc hộp thoại PIN bật lên.
        /// </summary>
        public const int PreflightTestDataSize = 32;


        public static readonly string[] HardwareKeyProviderMarkers =
        {
            "smart card",
            "bit4id",
            "vgca",
            "token",
            "etoken",
            "safenet",
            "gemalto",
            "thales",
            "epass",
            "feitian",
        };

        public static readonly string[] SoftwareKeyProviderMarkers =
        {
            "microsoft software key storage provider",
            "microsoft enhanced cryptographic provider",
            "microsoft base cryptographic provider",
            "microsoft strong cryptographic provider",
            "microsoft enhanced rsa and aes cryptographic provider",
        };
    }
}
