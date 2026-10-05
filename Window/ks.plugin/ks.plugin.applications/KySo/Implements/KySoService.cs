using ks.plugin.applications.KySo.Dtos;
using ks.plugin.applications.KySo.Interfaces;
using ks.plugin.external.Signing.Dtos;
using ks.plugin.external.Signing.Interfaces;
using Microsoft.Extensions.Logging;

namespace ks.plugin.applications.KySo.Implements
{
    public class KySoService : IKySoService
    {
        private readonly ISigningSession _signingSession;
        private readonly ILogger<KySoService> _logger;

        public KySoService(ISigningSession signingSession, ILogger<KySoService> logger)
        {
            _signingSession = signingSession;
            _logger = logger;
        }

        public MoPhienKetQuaDto OpenSession(MoPhienDto input) => _signingSession.Open(input.Thumbprint);

        public List<KetQuaKyDto> Sign(KyLoDto input)
        {
            _logger.LogInformation("{Method} requestCount={RequestCount}", nameof(Sign), input.YeuCau.Count);

            var results = new List<KetQuaKyDto>(input.YeuCau.Count);

            foreach (var request in input.YeuCau)
            {
                try
                {
                    var signature = _signingSession.Sign(Convert.FromBase64String(request.DuLieuBase64));
                    results.Add(new KetQuaKyDto
                    {
                        YeuCauId = request.YeuCauId,
                        ChuKyBase64 = Convert.ToBase64String(signature),
                    });
                }
                catch (Exception ex)
                {
                    // Ghi lý do chứ KHÔNG ghi dữ liệu đem ký: nhật ký của plugin không được chứa nội dung.
                    _logger.LogWarning(ex, "Ký yêu cầu {YeuCauId} thất bại", request.YeuCauId);
                    results.Add(new KetQuaKyDto { YeuCauId = request.YeuCauId, Loi = ex.Message });
                }
            }

            return results;
        }

        public void CloseSession() => _signingSession.Close();
    }
}
