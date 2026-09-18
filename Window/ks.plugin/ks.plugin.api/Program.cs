using ks.plugin.applications.ChungThuSo.Implements;
using ks.plugin.applications.ChungThuSo.Interfaces;
using ks.plugin.applications.Plugin.Implements;
using ks.plugin.applications.Plugin.Interfaces;
using ks.plugin.applications.KySo.Implements;
using ks.plugin.applications.KySo.Interfaces;
using ks.plugin.external.CauHinh.Implements;
using ks.plugin.external.CauHinh.Interfaces;
using ks.plugin.external.Certificates.Implements;
using ks.plugin.external.Certificates.Interfaces;
using ks.plugin.external.Signing.Implements;
using ks.plugin.external.Signing.Interfaces;
using ks.plugin.external.Tray.Implements;
using ks.plugin.external.Tray.Interfaces;
using ks.plugin.shared.Constants;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace ks.plugin.api
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Cửa sổ console mặc định dùng bảng mã cũ, tiếng Việt ra dấu hỏi. Đặt trước mọi dòng in ra.
            Console.OutputEncoding = Encoding.UTF8;

            using IMotBanChay motBanChay = new MotBanChay();
            ICuaSoConsole cuaSoConsole = new CuaSoConsole();

            if (!motBanChay.GiuCho())
            {
                motBanChay.GoiBanDangChay();
                return;
            }

            cuaSoConsole.GoNutDong();
            cuaSoConsole.An();
            motBanChay.LangNgheYeuCauMo(cuaSoConsole.Hien);

            ICauHinhCaiDat cauHinhCaiDat = new CauHinhCaiDat();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                EnvironmentName = cauHinhCaiDat.DocMoiTruong()
            });

            // Chỉ nghe trên loopback: plugin phục vụ đúng trình duyệt của máy này, không lộ ra mạng LAN.
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(PluginConstants.Port);
            });

            // Danh sách ghim trong mã là nguồn chính vì bản phát hành không kèm file cấu hình; appsettings.json
            // chỉ để bổ sung origin khi phát triển.
            var allowedOrigins = PluginConstants.OriginMacDinh
                .Concat(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            builder.Services.AddCors(options =>
            {
                // Origin không phải hàng rào bảo mật (curl đặt được tuỳ ý), nhưng thiếu CORS thì trình duyệt
                // không đọc được kết quả - đây là điều kiện để FE chạy, không phải lớp phòng thủ.
                options.AddDefaultPolicy(policy => policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
            });

            builder.Services.AddControllers();

            builder.Services.AddSingleton<ICertificateProvider, CertificateProvider>();
            builder.Services.AddSingleton<ITokenVerifier, TokenVerifier>();
            builder.Services.AddSingleton<IChungThuSoService, ChungThuSoService>();
            builder.Services.AddSingleton<IPluginService, PluginService>();

            // Phiên ký là Singleton vì nó GIỮ handle khoá đã mở: mỗi request một phiên mới thì lô nào cũng hỏi
            // PIN từng file.
            builder.Services.AddSingleton<ISigningSession, SigningSession>();
            builder.Services.AddSingleton<IKySoService, KySoService>();

            var app = builder.Build();

            app.UseCors();
            app.MapControllers();

            app.Logger.LogInformation("{Ten} {PhienBan} đang nghe tại http://127.0.0.1:{Port}",
                PluginConstants.Ten, PluginConstants.PhienBan, PluginConstants.Port);

            app.Start();

            using var luongBieuTuong = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(PluginConstants.TaiNguyenBieuTuong)!;
            using var bieuTuong = new Icon(luongBieuTuong, SystemInformation.SmallIconSize);

            IKhayHeThong khayHeThong = new KhayHeThong(cuaSoConsole);
            khayHeThong.Chay(bieuTuong, () => app.StopAsync().GetAwaiter().GetResult());
        }
    }
}
