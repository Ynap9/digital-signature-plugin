namespace ks.plugin.external.Signing.Dtos
{
    public class MoPhienKetQuaDto
    {
        public string Thumbprint { get; set; } = string.Empty;

        public string CommonName { get; set; } = string.Empty;

        public string ChungThuBase64 { get; set; } = string.Empty;
    }
}
