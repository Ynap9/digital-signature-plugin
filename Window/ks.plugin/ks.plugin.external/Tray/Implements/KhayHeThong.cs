using ks.plugin.external.Tray.Interfaces;
using ks.plugin.shared.Constants;
using System.Drawing;
using System.Windows.Forms;

namespace ks.plugin.external.Tray.Implements
{
    public class KhayHeThong : IKhayHeThong
    {
        private readonly ICuaSoConsole _cuaSoConsole;

        public KhayHeThong(ICuaSoConsole cuaSoConsole)
        {
            _cuaSoConsole = cuaSoConsole;
        }

        public void Chay(Icon bieuTuong, Action khiThoat)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var menu = new ContextMenuStrip();
            using var bieuTuongKhay = new NotifyIcon
            {
                Icon = bieuTuong,
                Text = $"{PluginConstants.Ten} {PluginConstants.PhienBan}",
                ContextMenuStrip = menu,
                Visible = true,
            };

            menu.Items.Add("Mở", null, (_, _) => _cuaSoConsole.Hien());
            menu.Items.Add("Thoát", null, (_, _) =>
            {
                bieuTuongKhay.Visible = false;
                khiThoat();
                Application.ExitThread();
            });

            bieuTuongKhay.MouseClick += (_, doiSo) =>
            {
                if (doiSo.Button == MouseButtons.Left)
                {
                    _cuaSoConsole.Hien();
                }
            };

            Application.Run();
        }
    }
}
