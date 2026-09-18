using ks.plugin.shared.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ks.plugin.api.Controllers.Base
{
    /// <summary>
    /// Nền chung của mọi controller: lỗi trả về vẫn là HTTP 200, trạng thái thật nằm trong envelope - FE đọc
    /// một kiểu cho cả plugin lẫn BE.
    /// </summary>
    [ApiController]
    public class BaseController : ControllerBase
    {
        protected readonly ILogger _logger;

        public BaseController(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>Ghi log rồi gói ngoại lệ thành envelope lỗi, không để stack trace lọt ra ngoài.</summary>
        protected ApiResponse OkException(Exception ex)
        {
            _logger.LogError(ex, $"{ex.GetType().Name}: {ex.Message}");
            return new ApiResponse(StatusCodeE.Error, null, 500, ex.Message);
        }
    }
}
