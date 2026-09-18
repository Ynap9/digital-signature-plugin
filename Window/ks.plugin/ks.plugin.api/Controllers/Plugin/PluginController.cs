using ks.plugin.api.Controllers.Base;
using ks.plugin.applications.Plugin.Interfaces;
using ks.plugin.shared.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ks.plugin.api.Controllers.Plugin
{
    /// <summary>
    /// Danh tính plugin. Đây là endpoint FE dò trước khi vào màn chọn chứng thư: gọi không tới nghĩa là máy
    /// chưa cài plugin, FE hiện popup tải bộ cài.
    /// </summary>
    [Route("api/plugin")]
    public class PluginController : BaseController
    {
        private readonly IPluginService _pluginService;

        public PluginController(IPluginService pluginService, ILogger<PluginController> logger) : base(logger)
        {
            _pluginService = pluginService;
        }

        /// <summary>Tên và phiên bản plugin đang chạy trên máy người dùng.</summary>
        [HttpGet("trang-thai")]
        public ApiResponse GetTrangThai()
        {
            try
            {
                var result = _pluginService.GetTrangThai();
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}
