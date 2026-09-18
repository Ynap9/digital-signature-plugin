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
        private readonly IKySoService _kySoService;

        public KySoController(IKySoService kySoService, ILogger<KySoController> logger) : base(logger)
        {
            _kySoService = kySoService;
        }

        /// <summary>Mở phiên ký cho cả lô. Hộp thoại nhập PIN bật lên ở đây và chỉ ở đây.</summary>
        [HttpPost("mo-phien")]
        public ApiResponse MoPhien([FromBody] MoPhienDto dto)
        {
            try
            {
                return new(_kySoService.MoPhien(dto));
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>Ký một lô yêu cầu bằng phiên đã mở.</summary>
        [HttpPost("ky")]
        public ApiResponse Ky([FromBody] KyLoDto dto)
        {
            try
            {
                return new(_kySoService.Ky(dto));
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Đo thời gian một lượt ký thật trên token. Mở phiên nên hộp PIN sẽ bật; dùng để biết sàn cứng của
        /// thiết bị trước khi bàn tối ưu phần mềm.
        /// </summary>
        [HttpPost("do-toc-do")]
        public ApiResponse DoTocDo([FromBody] DoTocDoDto dto)
        {
            try
            {
                return new(_kySoService.DoTocDo(dto));
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>Đóng phiên khi lô xong hoặc người dùng huỷ.</summary>
        [HttpPost("dong-phien")]
        public ApiResponse DongPhien()
        {
            try
            {
                _kySoService.DongPhien();
                return new(true);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}
