using ks.plugin.external.Tray.Interfaces;
using ks.plugin.shared.Constants;

namespace ks.plugin.external.Tray.Implements
{
    public class MotBanChay : IMotBanChay
    {
        private Mutex? _khoa;
        private EventWaitHandle? _tinHieu;

        public bool GiuCho()
        {
            _khoa = new Mutex(true, PluginConstants.TenKhoaMotBanChay, out var laBanDauTien);

            if (!laBanDauTien)
            {
                _khoa.Dispose();
                _khoa = null;
            }

            return laBanDauTien;
        }

        public void GoiBanDangChay()
        {
            if (!EventWaitHandle.TryOpenExisting(PluginConstants.TenTinHieuMoCuaSo, out var tinHieu))
            {
                return;
            }

            using (tinHieu)
            {
                tinHieu.Set();
            }
        }

        public void LangNgheYeuCauMo(Action moCuaSo)
        {
            _tinHieu = new EventWaitHandle(false, EventResetMode.AutoReset, PluginConstants.TenTinHieuMoCuaSo);

            var luong = new Thread(() =>
            {
                while (_tinHieu.WaitOne())
                {
                    moCuaSo();
                }
            })
            {
                IsBackground = true,
            };

            luong.Start();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            _tinHieu?.Dispose();
            _tinHieu = null;

            if (_khoa == null)
            {
                return;
            }

            _khoa.ReleaseMutex();
            _khoa.Dispose();
            _khoa = null;
        }
    }
}
