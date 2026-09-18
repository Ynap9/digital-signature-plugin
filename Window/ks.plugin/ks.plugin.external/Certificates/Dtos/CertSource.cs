namespace ks.plugin.external.Certificates.Dtos
{
    /// <summary>
    /// Nơi chứa chứng thư số. Đánh số TƯỜNG MINH vì giá trị đi thẳng ra JSON cho FE dưới dạng SỐ.
    /// </summary>
    public enum CertSource
    {
        /// <summary>Cert cài trong store cá nhân của tài khoản Windows đang chạy plugin.</summary>
        Local = 0,

        /// <summary>Cert cài ở store cấp máy.</summary>
        Server = 1,

        /// <summary>Khoá bí mật nằm ở provider PHẦN CỨNG - USB token hoặc smartcard.</summary>
        UsbToken = 2,
    }
}
