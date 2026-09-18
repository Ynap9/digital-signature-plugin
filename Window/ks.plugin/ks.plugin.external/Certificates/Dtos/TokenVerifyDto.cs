namespace ks.plugin.external.Certificates.Dtos
{
    public class TokenVerifyDto
    {
        public string Thumbprint { get; set; } = string.Empty;

        public string? CommonName { get; set; }

        public bool FoundInStore { get; set; }

        public bool HasPrivateKey { get; set; }

        public bool NotExpired { get; set; }

        public bool AllowsSigning { get; set; }

        public bool OnUsbToken { get; set; }

        public bool CanSignTest { get; set; }

        public bool Valid { get; set; }

        public string? Reason { get; set; }
    }
}
