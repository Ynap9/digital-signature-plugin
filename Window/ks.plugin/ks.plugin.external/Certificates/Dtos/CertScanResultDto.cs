namespace ks.plugin.external.Certificates.Dtos
{
    public class CertScanResultDto
    {
        public List<SignCertDto> Certificates { get; set; } = new();

        public List<string> StoreDiagnostics { get; set; } = new();
    }
}
