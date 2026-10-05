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
using ks.plugin.external.Logging.Implements;
using ks.plugin.external.Logging.Interfaces;
using ks.plugin.external.Signing.Implements;
using ks.plugin.external.Signing.Interfaces;
using ks.plugin.external.Tray.Implements;
using ks.plugin.external.Tray.Interfaces;
using ks.plugin.shared.Constants;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ks.plugin.api
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            using IMotBanChay singleInstance = new MotBanChay();

            if (!singleInstance.GiuCho())
            {
                singleInstance.GoiBanDangChay();
                return;
            }

            // Must run before any window is created.
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var iconBytes = ReadIcon();
            using var appIcon = new Icon(new MemoryStream(iconBytes));
            using var trayIcon = new Icon(new MemoryStream(iconBytes), SystemInformation.SmallIconSize);

            ILogBuffer logBuffer = new LogBuffer();

            ICauHinhCaiDat installConfig = new CauHinhCaiDat();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                EnvironmentName = installConfig.DocMoiTruong()
            });

            // Default providers include the Windows Event Log, which would persist thumbprints and failures to disk.
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new LogBufferProvider(logBuffer));
            // The release ships without appsettings.json, so its log levels must be set in code.
            builder.Logging.AddFilter<LogBufferProvider>("Microsoft.AspNetCore", LogLevel.Warning);
            // Its startup lines ("Press Ctrl+C to shut down") assume a console; the status cards show the same facts.
            builder.Logging.AddFilter<LogBufferProvider>("Microsoft.Hosting.Lifetime", LogLevel.Warning);

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

            app.Logger.LogInformation("{Name} {Version} đang nghe tại http://127.0.0.1:{Port}",
                PluginConstants.Ten, PluginConstants.PhienBan, PluginConstants.Port);

            try
            {
                app.Start();
            }
            catch (Exception ex)
            {
                // No console any more: without this dialog a startup failure (e.g. port taken) is silent.
                MessageBox.Show(
                    $"Không khởi động được {PluginConstants.Ten} trên cổng {PluginConstants.Port}.\n\n{ex.Message}",
                    PluginConstants.Ten, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Created only after the host has started: a WinForms control installs a sync context on this thread,
            // and the blocking Start() above must not run under it.
            using var statusWindow = new StatusWindow(appIcon, logBuffer);
            statusWindow.SetEnvironment(builder.Environment.EnvironmentName);
            singleInstance.LangNgheYeuCauMo(statusWindow.ShowWindow);

            IKhayHeThong tray = new KhayHeThong(statusWindow);
            tray.Run(trayIcon, () => app.StopAsync().GetAwaiter().GetResult());
        }

        private static byte[] ReadIcon()
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(PluginConstants.TaiNguyenBieuTuong)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
