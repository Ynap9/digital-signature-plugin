using ks.plugin.external.Certificates.Dtos;
using ks.plugin.external.Certificates.Interfaces;
using ks.plugin.shared.Constants;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ks.plugin.external.Certificates.Implements
{
    /// <summary>
    /// Kiểm tra chứng thư bằng cách ký thử. Không giữ lại handle khoá: một lần kiểm là một lần hỏi PIN, và
    /// giữ handle sống ngoài phạm vi một lô ký là mở đường cho tiến trình khác mượn khoá đã mở.
    /// </summary>
    public class TokenVerifier : ITokenVerifier
    {
        private static readonly StoreLocation[] Locations =
        {
            StoreLocation.CurrentUser,
            StoreLocation.LocalMachine,
        };

        private readonly ILogger<TokenVerifier> _logger;

        public TokenVerifier(ILogger<TokenVerifier> logger)
        {
            _logger = logger;
        }

        public TokenVerifyDto Verify(string thumbprint)
        {
            _logger.LogInformation($"{nameof(Verify)} thumbprint={thumbprint}");

            var dto = new TokenVerifyDto { Thumbprint = thumbprint ?? string.Empty };

            X509Certificate2? cert = null;
            foreach (var location in Locations)
            {
                try
                {
                    using var store = new X509Store(StoreName.My, location);
                    store.Open(OpenFlags.ReadOnly);
                    cert = store.Certificates
                        .OfType<X509Certificate2>()
                        .FirstOrDefault(x => string.Equals(x.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    cert = null;
                }

                if (cert != null)
                {
                    break;
                }
            }

            if (cert == null)
            {
                dto.Reason = "Không tìm thấy chứng thư số trên máy. Kiểm tra USB token đã cắm và middleware bit4id đã chạy chưa.";
                return dto;
            }

            dto.FoundInStore = true;
            dto.CommonName = cert.GetNameInfo(X509NameType.SimpleName, false);

            if (!cert.HasPrivateKey)
            {
                dto.Reason = "Chứng thư số không có khoá bí mật trên máy này nên không ký được.";
                return dto;
            }
            dto.HasPrivateKey = true;

            var nowUtc = DateTime.UtcNow;
            if (nowUtc < cert.NotBefore.ToUniversalTime() || nowUtc > cert.NotAfter.ToUniversalTime())
            {
                dto.Reason = $"Chứng thư số ngoài thời hạn hiệu lực ({cert.NotBefore:dd/MM/yyyy HH:mm:ss} - {cert.NotAfter:dd/MM/yyyy HH:mm:ss}).";
                return dto;
            }
            dto.NotExpired = true;

            var keyUsage = cert.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
            if (keyUsage != null
                && !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature)
                && !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation))
            {
                dto.Reason = "Chứng thư số không được cấp quyền ký (KeyUsage thiếu digitalSignature/nonRepudiation).";
                return dto;
            }
            dto.AllowsSigning = true;

            try
            {
                var probe = RandomNumberGenerator.GetBytes(ChungThuSoConstants.PreflightTestDataSize);
                using var rsa = cert.GetRSAPrivateKey();
                if (rsa != null)
                {
                    dto.OnUsbToken = KeyProviders.IsHardwareProvider(KeyProviders.GetProviderName(rsa));
                    rsa.SignData(probe, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
                else
                {
                    using var ecdsa = cert.GetECDsaPrivateKey()
                        ?? throw new InvalidOperationException("Chứng thư số không dùng thuật toán RSA hoặc ECDSA.");
                    dto.OnUsbToken = KeyProviders.IsHardwareProvider(KeyProviders.GetProviderName(ecdsa));
                    ecdsa.SignData(probe, HashAlgorithmName.SHA256);
                }
            }
            catch (Exception ex)
            {
                dto.Reason = $"Không dùng được khoá bí mật để ký (token chưa cắm, PIN bị huỷ, hoặc middleware chưa sẵn sàng): {ex.Message}";
                return dto;
            }
            dto.CanSignTest = true;

            if (!dto.OnUsbToken)
            {
                dto.Reason = "Khoá bí mật nằm trong kho phần mềm của máy, không phải USB token - không dùng để ký.";
                return dto;
            }

            dto.Valid = true;
            return dto;
        }
    }
}
