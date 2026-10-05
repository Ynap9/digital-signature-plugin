using ks.plugin.api.Controllers.Base;
using ks.plugin.applications.KySo.Dtos;
using ks.plugin.applications.KySo.Interfaces;
using ks.plugin.shared.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ks.plugin.api.Controllers.KySo
{
    /// <summary>
    /// Ký hộ máy chủ bằng khoá trên token. Trang web đứng giữa: nó lấy yêu cầu ký từ máy chủ, đưa xuống đây,
    /// rồi mang chữ ký trả về. Máy chủ không bao giờ nhìn thấy khoá bí mật lẫn mã PIN.
    /// </summary>
    [Route("api/plugin/ky-so")]
    public class KySoController : BaseController
    {
        private readonly IKySoService _signingService;

        public KySoController(IKySoService signingService, ILogger<KySoController> logger) : base(logger)
        {
            _signingService = signingService;
        }

        /// <summary>Mở phiên ký cho cả lô. Hộp thoại nhập PIN bật lên ở đây và chỉ ở đây.</summary>
        [HttpPost("mo-phien")]
        public ApiResponse OpenSession([FromBody] MoPhienDto dto)
        {
            try
            {
                return new(_signingService.OpenSession(dto));
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>Ký một lô yêu cầu bằng phiên đã mở.</summary>
        [HttpPost("ky")]
        public ApiResponse Sign([FromBody] KyLoDto dto)
        {
            try
            {
                return new(_signingService.Sign(dto));
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>Đóng phiên khi lô xong hoặc người dùng huỷ.</summary>
        [HttpPost("dong-phien")]
        public ApiResponse CloseSession()
        {
            try
            {
                _signingService.CloseSession();
                return new(true);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}
