using ks.plugin.external.Certificates.Implements;
using ks.plugin.external.Signing.Dtos;
using ks.plugin.external.Signing.Interfaces;
using ks.plugin.shared.Constants;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ks.plugin.external.Signing.Implements
{
    public class SigningSession : ISigningSession
    {
        private static readonly StoreLocation[] Locations =
        {
            StoreLocation.CurrentUser,
            StoreLocation.LocalMachine,
        };

        private readonly ILogger<SigningSession> _logger;

        /// <summary>Một máy chỉ có một người ngồi ký, nên phiên là duy nhất và mọi lượt ký xếp hàng qua khoá này.</summary>
        private readonly object _khoa = new();

        private X509Certificate2? _cert;

        /// <summary>
        /// Handle khoá đã mở, giữ suốt phiên. Đây mới là thứ "giữ handle" thật sự: mở khoá cho từng chữ ký
        /// là mỗi lần một vòng mở phiên với thẻ, và tuỳ middleware còn kèm xác thực PIN lại — đo thực trên
        /// token bit4id là 1,5 giây mỗi chữ ký thay vì khoảng 200 ms.
        /// </summary>
        private RSA? _rsa;
        private ECDsa? _ecdsa;

        private DateTime _lanDungCuoi;

        public SigningSession(ILogger<SigningSession> logger)
        {
            _logger = logger;
        }

        public MoPhienKetQuaDto MoPhien(string thumbprint)
        {
            _logger.LogInformation("{Method} thumbprint={Thumbprint}", nameof(MoPhien), thumbprint);

            lock (_khoa)
            {
                DongPhienTrongKhoa();

                var cert = TimChungThu(thumbprint)
                    ?? throw new InvalidOperationException(
                        "Không tìm thấy chứng thư số theo vân tay đã chọn. Kiểm tra token đã cắm chưa.");

                _cert = cert;
                _rsa = cert.GetRSAPrivateKey();
                _ecdsa = _rsa == null ? cert.GetECDsaPrivateKey() : null;

                if (_rsa == null && _ecdsa == null)
                {
                    DongPhienTrongKhoa();
                    throw new InvalidOperationException("Chứng thư số không dùng thuật toán RSA hoặc ECDSA.");
                }

                // Ký thử một mẩu ngẫu nhiên để BUỘC middleware mở khoá ngay bây giờ: hộp PIN bật đúng ở đây,
                // thay vì bật giữa chừng khi lô đã chạy được vài trăm file.
                KyBangHandle(RandomNumberGenerator.GetBytes(ChungThuSoConstants.PreflightTestDataSize));

                _lanDungCuoi = DateTime.UtcNow;

                return new MoPhienKetQuaDto
                {
                    Thumbprint = cert.Thumbprint,
                    CommonName = cert.GetNameInfo(X509NameType.SimpleName, false),
                    ChungThuBase64 = Convert.ToBase64String(cert.RawData),
                };
            }
        }

        public byte[] Ky(byte[] duLieu)
        {
            lock (_khoa)
            {
                if (_cert != null && DateTime.UtcNow - _lanDungCuoi > TimeSpan.FromMinutes(KySoConstants.PhutTuDongDongPhien))
                {
                    _logger.LogInformation("Phiên ký quá hạn không dùng, tự đóng");
                    DongPhienTrongKhoa();
                }

                if (_cert == null)
                {
                    throw new InvalidOperationException("Chưa mở phiên ký, hoặc phiên đã đóng vì để lâu không dùng.");
                }

                var chuKy = KyBangHandle(duLieu);
                _lanDungCuoi = DateTime.UtcNow;
                return chuKy;
            }
        }

        public void DongPhien()
        {
            lock (_khoa)
            {
                DongPhienTrongKhoa();
            }
        }

        public DoTocDoKetQuaDto DoTocDo(string thumbprint, int soLan)
        {
            _logger.LogInformation("{Method} thumbprint={Thumbprint} soLan={SoLan}",
                nameof(DoTocDo), thumbprint, soLan);

            MoPhien(thumbprint);

            lock (_khoa)
            {
                var mau = RandomNumberGenerator.GetBytes(ChungThuSoConstants.PreflightTestDataSize);
                var moc = new List<double>(soLan);

                for (var i = 0; i < soLan; i++)
                {
                    var dongHo = System.Diagnostics.Stopwatch.StartNew();
                    KyBangHandle(mau);
                    moc.Add(dongHo.Elapsed.TotalMilliseconds);
                }

                _lanDungCuoi = DateTime.UtcNow;

                return new DoTocDoKetQuaDto
                {
                    SoLan = soLan,
                    TrungBinhMs = Math.Round(moc.Average(), 1),
                    NhanhNhatMs = Math.Round(moc.Min(), 1),
                    ChamNhatMs = Math.Round(moc.Max(), 1),
                    KichThuocKhoaBit = _rsa?.KeySize ?? _ecdsa?.KeySize ?? 0,
                    ThuatToan = _rsa != null ? "RSA" : "ECDSA",
                    TenProvider = TenProvider(),
                };
            }
        }

        /// <summary>Tên provider mật mã đang giữ khoá — cho biết đang đi qua middleware nào của hãng token.</summary>
        public string TenProvider()
        {
            return _rsa is RSACng rsaCng
                ? rsaCng.Key.Provider?.Provider ?? string.Empty
                : _ecdsa is ECDsaCng ecdsaCng ? ecdsaCng.Key.Provider?.Provider ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Giải phóng handle khoá. Chỉ gọi khi đã giữ khoá - phiên là tài nguyên dùng chung của cả tiến trình.
        /// </summary>
        public void DongPhienTrongKhoa()
        {
            _rsa?.Dispose();
            _rsa = null;
            _ecdsa?.Dispose();
            _ecdsa = null;
            _cert?.Dispose();
            _cert = null;
        }

        /// <summary>Tìm chứng thư kèm khoá riêng theo vân tay, quét cả kho của người dùng lẫn của máy.</summary>
        public X509Certificate2? TimChungThu(string thumbprint)
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
        public byte[] KyBangHandle(byte[] duLieu)
        {
            if (_rsa != null)
            {
                return _rsa.SignData(duLieu, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }

            return _ecdsa!.SignData(duLieu, HashAlgorithmName.SHA256);
        }
    }
}
