using ks.plugin.applications.ChungThuSo.Interfaces;
using ks.plugin.external.Certificates.Dtos;
using ks.plugin.external.Certificates.Interfaces;

namespace ks.plugin.applications.ChungThuSo.Implements
{
    public class ChungThuSoService : IChungThuSoService
    {
        private readonly ICertificateProvider _certificateProvider;
        private readonly ITokenVerifier _tokenVerifier;

        public ChungThuSoService(ICertificateProvider certificateProvider, ITokenVerifier tokenVerifier)
        {
            _certificateProvider = certificateProvider;
            _tokenVerifier = tokenVerifier;
        }

        public CertScanResultDto GetList(bool onlySignable)
        {
            var result = _certificateProvider.GetCertificates();

            if (onlySignable)
            {
                result.Certificates = result.Certificates.Where(x => x.Reason == null).ToList();
            }

            return result;
        }

        public TokenVerifyDto KiemTraToken(string thumbprint)
        {
            return _tokenVerifier.Verify(thumbprint);
        }
    }
}
