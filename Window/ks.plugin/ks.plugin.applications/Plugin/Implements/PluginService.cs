using ks.plugin.applications.Plugin.Dtos;
using ks.plugin.applications.Plugin.Interfaces;
using ks.plugin.shared.Constants;

namespace ks.plugin.applications.Plugin.Implements
{
    public class PluginService : IPluginService
    {
        public ViewTrangThaiDto GetTrangThai()
        {
            return new ViewTrangThaiDto
            {
                Ten = PluginConstants.Ten,
                PhienBan = PluginConstants.PhienBan,
                SanSang = true,
            };
        }
    }
}
