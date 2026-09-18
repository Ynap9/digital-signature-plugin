namespace ks.plugin.external.Tray.Interfaces
{
    public interface IMotBanChay : IDisposable
    {
        bool GiuCho();

        void GoiBanDangChay();

        void LangNgheYeuCauMo(Action moCuaSo);
    }
}
