namespace forzion.tech.Infrastructure.Services;

public enum NfseAmbiente
{
    Restrita = 0,
    Producao = 1
}

public class NfseSettings
{
    public bool Habilitado { get; set; }
    public NfseAmbiente Ambiente { get; set; } = NfseAmbiente.Restrita;
    public string UrlBase { get; set; } = string.Empty;
    public string CnpjPrestador { get; set; } = string.Empty;
    public string InscricaoMunicipal { get; set; } = string.Empty;
    public string CodigoMunicipioIbge { get; set; } = string.Empty;
    public string SerieDps { get; set; } = string.Empty;
    public string CodigoServicoAssinatura { get; set; } = string.Empty;
    public string CodigoServicoComissao { get; set; } = string.Empty;
    public decimal AliquotaIss { get; set; }
    public string CertificadoPath { get; set; } = string.Empty;
    public string CertificadoSenha { get; set; } = string.Empty;
    public string RegimeTributario { get; set; } = "SimplesNacional";
    public int PrazoCancelamentoDias { get; set; } = 90;
    public string TribISSQN { get; set; } = "1";
    public string TpRetISSQN { get; set; } = "1";
}
