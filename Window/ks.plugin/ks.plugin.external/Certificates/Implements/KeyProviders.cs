using ks.plugin.shared.Constants;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ks.plugin.external.Certificates.Implements
{
    public static class KeyProviders
    {
        /// <summary>
        /// Reads key metadata only, so no PIN prompt. Legacy tokens (SafeNet eToken via TokenManager) expose
        /// keys through a CAPI CSP rather than a CNG KSP, so both kinds must be recognised.
        /// </summary>
        public static string? GetProviderName(X509Certificate2 cert)
        {
            if (!cert.HasPrivateKey)
            {
                return null;
            }

            try
            {
                using var rsa = cert.GetRSAPrivateKey();
                if (rsa != null)
                {
                    return GetProviderName(rsa);
                }

                using var ecdsa = cert.GetECDsaPrivateKey();
                return ecdsa != null ? GetProviderName(ecdsa) : null;
            }
            catch
            {
                return null;
            }
        }

        public static string? GetProviderName(AsymmetricAlgorithm key)
        {
            return key switch
            {
                RSACng rsaCng => rsaCng.Key.Provider?.Provider,
                ECDsaCng ecdsaCng => ecdsaCng.Key.Provider?.Provider,
#pragma warning disable SYSLIB0028
                RSACryptoServiceProvider rsaCapi => rsaCapi.CspKeyContainerInfo.ProviderName,
#pragma warning restore SYSLIB0028
                _ => null,
            };
        }

        public static bool IsHardwareProvider(string? providerName)
        {
            return !string.IsNullOrWhiteSpace(providerName)
                && !ChungThuSoConstants.SoftwareKeyProviderMarkers.Any(marker =>
                    providerName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                && ChungThuSoConstants.HardwareKeyProviderMarkers.Any(marker =>
                    providerName.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }
    }
}
