using FluentAssertions;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class NfseSettingsTests
{
    private static readonly Dictionary<string, string?> ConfigCompleta = new()
    {
        ["Nfse:Habilitado"] = "true",
        ["Nfse:CertificadoPath"] = "/secrets/cert.pfx",
        ["Nfse:CertificadoSenha"] = "senha-secreta",
        ["Nfse:CnpjPrestador"] = "11222333000181",
        ["Nfse:InscricaoMunicipal"] = "123456",
        ["Nfse:CodigoMunicipioIbge"] = "3550308",
        ["Nfse:SerieDps"] = "1",
        ["Nfse:CodigoServicoAssinatura"] = "1.05",
        ["Nfse:AliquotaIss"] = "2.0"
    };

    [Fact]
    public void HabilitadoSemCertificado_FalhaBoot()
    {
        using var sp = InfraHarness.BuildProvider("Development",
            new Dictionary<string, string?> { ["Nfse:Habilitado"] = "true" });

        var act = () => _ = sp.GetRequiredService<IOptions<NfseSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Desabilitado_SobeComDefaults()
    {
        using var sp = InfraHarness.BuildProvider("Development");

        var settings = sp.GetRequiredService<IOptions<NfseSettings>>().Value;

        settings.Habilitado.Should().BeFalse();
        settings.Ambiente.Should().Be(NfseAmbiente.Restrita);
    }

    [Fact]
    public void HabilitadoComConfigCompleta_Sobe()
    {
        using var sp = InfraHarness.BuildProvider("Development", ConfigCompleta);

        var settings = sp.GetRequiredService<IOptions<NfseSettings>>().Value;

        settings.Habilitado.Should().BeTrue();
        settings.CnpjPrestador.Should().Be("11222333000181");
        settings.AliquotaIss.Should().Be(2.0m);
    }

    [Fact]
    public void HabilitadoSemAliquota_FalhaBoot()
    {
        var config = new Dictionary<string, string?>(ConfigCompleta) { ["Nfse:AliquotaIss"] = "0" };
        using var sp = InfraHarness.BuildProvider("Development", config);

        var act = () => _ = sp.GetRequiredService<IOptions<NfseSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
