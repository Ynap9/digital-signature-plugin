using ks.plugin.external.Signing.Dtos;
using ks.plugin.external.Signing.Interfaces;
using ks.plugin.shared.Constants;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ks.plugin.external.Signing.Implements
{
    public class SigningSession : ISigningSession, IDisposable
    {
        private static readonly StoreLocation[] Locations =
        {
            StoreLocation.CurrentUser,
            StoreLocation.LocalMachine,
        };

        private const string TokenRemovedMessage =
            "Phiên ký đã đóng vì USB token đã bị rút. Cắm lại token rồi mở phiên mới.";

        private static readonly string IdleTimeoutMessage =
            $"Phiên ký đã đóng vì để quá {KySoConstants.IdleTimeoutMinutes} phút không dùng.";

        private readonly ILogger<SigningSession> _logger;

        /// <summary>Một máy chỉ có một người ngồi ký, nên phiên là duy nhất và mọi lượt ký xếp hàng qua khoá này.</summary>
        private readonly object _lock = new();

        private readonly System.Threading.Timer _checkTimer;

        private X509Certificate2? _cert;

        /// <summary>
        /// Handle khoá đã mở, giữ suốt phiên. Đây mới là thứ "giữ handle" thật sự: mở khoá cho từng chữ ký
        /// là mỗi lần một vòng mở phiên với thẻ, và tuỳ middleware còn kèm xác thực PIN lại — đo thực trên
        /// token bit4id là 1,5 giây mỗi chữ ký thay vì khoảng 200 ms.
        /// </summary>
        private RSA? _rsa;
        private ECDsa? _ecdsa;

        private DateTime _lastUsed;

        private string? _closedReason;

        public SigningSession(ILogger<SigningSession> logger)
        {
            _logger = logger;

            var interval = TimeSpan.FromSeconds(KySoConstants.SessionCheckSeconds);
            _checkTimer = new System.Threading.Timer(_ => CheckSession(), null, interval, interval);
        }

        public MoPhienKetQuaDto Open(string thumbprint)
        {
            _logger.LogInformation("{Method} thumbprint={Thumbprint}", nameof(Open), thumbprint);

            lock (_lock)
            {
                CloseLocked(null);

                var cert = FindCertificate(thumbprint)
                    ?? throw new InvalidOperationException(
                        "Không tìm thấy chứng thư số theo vân tay đã chọn. Kiểm tra token đã cắm chưa.");

                _cert = cert;
                _rsa = cert.GetRSAPrivateKey();
                _ecdsa = _rsa == null ? cert.GetECDsaPrivateKey() : null;

                if (_rsa == null && _ecdsa == null)
                {
                    CloseLocked(null);
                    throw new InvalidOperationException("Chứng thư số không dùng thuật toán RSA hoặc ECDSA.");
                }

                // Ký thử một mẩu ngẫu nhiên để BUỘC middleware mở khoá ngay bây giờ: hộp PIN bật đúng ở đây,
                // thay vì bật giữa chừng khi lô đã chạy được vài trăm file.
                try
                {
                    SignWithHandle(RandomNumberGenerator.GetBytes(ChungThuSoConstants.PreflightTestDataSize));
                }
                catch
                {
                    CloseLocked(null);
                    throw;
                }

                _lastUsed = DateTime.UtcNow;

                return new MoPhienKetQuaDto
                {
                    Thumbprint = cert.Thumbprint,
                    CommonName = cert.GetNameInfo(X509NameType.SimpleName, false),
                    ChungThuBase64 = Convert.ToBase64String(cert.RawData),
                };
            }
        }

        public byte[] Sign(byte[] data)
        {
            lock (_lock)
            {
                if (_cert != null && IsIdleExpired())
                {
                    _logger.LogInformation("Phiên ký quá hạn không dùng, tự đóng");
                    CloseLocked(IdleTimeoutMessage);
                }

                if (_cert == null)
                {
                    throw new InvalidOperationException(_closedReason ?? "Chưa mở phiên ký.");
                }

                byte[] signature;
                try
                {
                    signature = SignWithHandle(data);
                }
                catch (CryptographicException) when (!IsInStore(_cert.Thumbprint))
                {
                    _logger.LogWarning("Ký thất bại vì token đã rút, đóng phiên thumbprint={Thumbprint}", _cert.Thumbprint);
                    CloseLocked(TokenRemovedMessage);
                    throw new InvalidOperationException(TokenRemovedMessage);
                }

                _lastUsed = DateTime.UtcNow;
                return signature;
            }
        }

        public void Close()
        {
            lock (_lock)
            {
                CloseLocked(null);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            _checkTimer.Dispose();
            Close();
        }

        private void CheckSession()
        {
            if (!Monitor.TryEnter(_lock))
            {
                return;
            }

            try
            {
                if (_cert == null)
                {
                    return;
                }

                if (IsIdleExpired())
                {
                    _logger.LogInformation("Phiên ký quá hạn không dùng, tự đóng");
                    CloseLocked(IdleTimeoutMessage);
                    return;
                }

                if (!IsInStore(_cert.Thumbprint))
                {
                    _logger.LogWarning("Token đã rút, đóng phiên thumbprint={Thumbprint}", _cert.Thumbprint);
                    CloseLocked(TokenRemovedMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kiểm tra phiên ký thất bại");
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }

        private bool IsIdleExpired() =>
            DateTime.UtcNow - _lastUsed > TimeSpan.FromMinutes(KySoConstants.IdleTimeoutMinutes);

        private static bool IsInStore(string thumbprint)
        {
            foreach (var location in Locations)
            {
                try
                {
                    using var store = new X509Store(StoreName.My, location);
                    store.Open(OpenFlags.ReadOnly);

                    var all = store.Certificates;
                    var found = all.Find(X509FindType.FindByThumbprint, thumbprint, false);
                    var isFound = found.Count > 0;

                    foreach (var cert in all)
                    {
                        cert.Dispose();
                    }
                    foreach (var cert in found)
                    {
                        cert.Dispose();
                    }

                    if (isFound)
                    {
                        return true;
                    }
                }
                catch (CryptographicException)
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Giải phóng handle khoá. Chỉ gọi khi đã giữ khoá - phiên là tài nguyên dùng chung của cả tiến trình.
        /// </summary>
        private void CloseLocked(string? reason)
        {
            _rsa?.Dispose();
            _rsa = null;
            _ecdsa?.Dispose();
            _ecdsa = null;
            _cert?.Dispose();
            _cert = null;
            _closedReason = reason;
        }

        /// <summary>Tìm chứng thư kèm khoá riêng theo vân tay, quét cả kho của người dùng lẫn của máy.</summary>
        private X509Certificate2? FindCertificate(string thumbprint)
        {
            foreach (var location in Locations)
            {
                try
                {
                    using var store = new X509Store(StoreName.My, location);
                    store.Open(OpenFlags.ReadOnly);

                    var found = store.Certificates
                        .Find(X509FindType.FindByThumbprint, thumbprint, false)
                        .FirstOrDefault(x => x.HasPrivateKey);

                    if (found != null)
                    {
                        return found;
                    }
                }
                catch (CryptographicException ex)
                {
                    // Kho của máy thường không mở được khi chạy quyền người dùng thường - bỏ qua kho đó.
                    _logger.LogWarning(ex, "Không mở được kho chứng thư {Location}", location);
                }
            }

            return null;
        }

        /// <summary>
        /// Ký SHA-256 bằng handle khoá đã mở sẵn của phiên. Nhận cả RSA lẫn ECDSA vì chứng thư của các nhà
        /// cung cấp trong nước dùng cả hai; thuật toán phải khớp với bên máy chủ lắp CMS.
        /// </summary>
        private byte[] SignWithHandle(byte[] data)
        {
            if (_rsa != null)
            {
                return _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }

            return _ecdsa!.SignData(data, HashAlgorithmName.SHA256);
        }
    }
}
