using ks.plugin.api.Controllers.Base;
using ks.plugin.applications.ChungThuSo.Dtos;
using ks.plugin.applications.ChungThuSo.Interfaces;
using ks.plugin.shared.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ks.plugin.api.Controllers.ChungThuSo
{
    /// <summary>
    /// Chứng thư số trên máy người dùng - nguồn thật, khác với endpoint cùng tên bên BE vốn chỉ đọc được
    /// store của máy chạy API.
    /// </summary>
    [Route("api/plugin/chung-thu-so")]
    public class ChungThuSoController : BaseController
    {
        private readonly IChungThuSoService _chungThuSoService;

        public ChungThuSoController(IChungThuSoService chungThuSoService,
            ILogger<ChungThuSoController> logger) : base(logger)
        {
            _chungThuSoService = chungThuSoService;
        }

        /// <summary>Liệt kê chứng thư; onlySignable=true để bỏ các cert không ký được.</summary>
        [HttpGet]
        public ApiResponse GetList([FromQuery] bool onlySignable = false)
        {
            try
            {
                var result = _chungThuSoService.GetList(onlySignable);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Kiểm tra chứng thư đã chọn bằng cách ký thử. Hộp thoại nhập PIN của bit4id bật lên ở bước này,
        /// nên đừng gọi lúc chỉ đang hiển thị danh sách.
        /// </summary>
        [HttpPost("kiem-tra-token")]
        public ApiResponse KiemTraToken([FromBody] KiemTraTokenDto dto)
        {
            try
            {
                var result = _chungThuSoService.KiemTraToken(dto.Thumbprint);
                return new(result);
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }
    }
}
