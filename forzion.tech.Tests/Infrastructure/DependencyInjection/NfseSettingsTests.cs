using FluentAssertions;
using forzion.tech.Infrastructure.DependencyInjection;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

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

    private static ServiceProvider BuildProvider(Dictionary<string, string?>? config = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddInfrastructure(configuration, Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Development"));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void HabilitadoSemCertificado_FalhaBoot()
    {
        using var sp = BuildProvider(new Dictionary<string, string?> { ["Nfse:Habilitado"] = "true" });

        var act = () => _ = sp.GetRequiredService<IOptions<NfseSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Desabilitado_SobeComDefaults()
    {
        using var sp = BuildProvider();

        var settings = sp.GetRequiredService<IOptions<NfseSettings>>().Value;

        settings.Habilitado.Should().BeFalse();
        settings.Ambiente.Should().Be(NfseAmbiente.Restrita);
    }

    [Fact]
    public void HabilitadoComConfigCompleta_Sobe()
    {
        using var sp = BuildProvider(ConfigCompleta);

        var settings = sp.GetRequiredService<IOptions<NfseSettings>>().Value;

        settings.Habilitado.Should().BeTrue();
        settings.CnpjPrestador.Should().Be("11222333000181");
        settings.AliquotaIss.Should().Be(2.0m);
    }

    [Fact]
    public void HabilitadoSemAliquota_FalhaBoot()
    {
        var config = new Dictionary<string, string?>(ConfigCompleta) { ["Nfse:AliquotaIss"] = "0" };
        using var sp = BuildProvider(config);

        var act = () => _ = sp.GetRequiredService<IOptions<NfseSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
