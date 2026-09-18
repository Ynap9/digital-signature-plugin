namespace ks.plugin.shared.Constants
{
    /// <summary>Hằng số của phiên ký giữ handle khoá trên token.</summary>
    public static class KySoConstants
    {
        /// <summary>
        /// Phiên tự đóng sau ngần này phút không dùng. Handle khoá đã mở là thứ nhạy cảm nhất tiến trình này
        /// giữ, nên không để nó sống mãi chỉ vì người dùng quên đóng tab; lô đang chạy thì có lượt ký liên
        /// tục nên không bao giờ chạm mốc này.
        /// </summary>
        public const int PhutTuDongDongPhien = 15;

        /// <summary>Trần số lượt ký khi đo tốc độ: đo là chạm vào token thật, không để ai gọi thành vòng lặp vô tận.</summary>
        public const int SoLanDoToiDa = 100;
    }
}
