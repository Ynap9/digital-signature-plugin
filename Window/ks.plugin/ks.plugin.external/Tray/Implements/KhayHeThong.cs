using ks.plugin.external.Tray.Interfaces;
using ks.plugin.shared.Constants;
using System.Drawing;
using System.Windows.Forms;

namespace ks.plugin.external.Tray.Implements
{
    public class KhayHeThong : IKhayHeThong
    {
        private readonly IStatusWindow _statusWindow;

        public KhayHeThong(IStatusWindow statusWindow)
        {
            _statusWindow = statusWindow;
        }

        public void Run(Icon icon, Action onExit)
        {
            using var menu = new ContextMenuStrip();
            using var trayIcon = new NotifyIcon
            {
                Icon = icon,
                Text = $"{PluginConstants.Ten} {PluginConstants.PhienBan}",
                ContextMenuStrip = menu,
                Visible = true,
            };

            menu.Items.Add("Mở", null, (_, _) => _statusWindow.ShowWindow());
            menu.Items.Add("Thoát", null, (_, _) =>
            {
                trayIcon.Visible = false;
                onExit();
                Application.ExitThread();
            });

            trayIcon.MouseClick += (_, args) =>
            {
                if (args.Button == MouseButtons.Left)
                {
                    _statusWindow.ShowWindow();
                }
            };

            Application.Run();
        }
    }
}
