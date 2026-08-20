using Microsoft.AspNetCore.Builder;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Api.Configuration;

public class AgentsHmacExtensionsTests
{
    private const string JwtSecretDeTeste = "segredo-de-teste-com-pelo-menos-32-bytes!!";
    private const string SegredoDeTrintaEUmBytes = "1234567890123456789012345678901";

    private static WebApplicationBuilder CriarHost(string ambiente, params (string Chave, string? Valor)[] configuracao)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = ambiente });
        builder.Configuration.AddInMemoryCollection(
            configuracao.ToDictionary(par => par.Chave, par => par.Valor));
        return builder;
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Homolog")]
    public void AddAgentsHmac_AmbienteQueExigeSegredo_SegredoAusente_DerrubaOBoot(string ambiente)
    {
        var builder = CriarHost(ambiente);

        var act = () => builder.Services.AddAgentsHmac(builder.Configuration, builder.Environment);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'Agents:Hmac:SecretAtual'*")
            .WithMessage("*openssl rand -base64 64*");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Homolog")]
    public void AddAgentsHmac_AmbienteQueExigeSegredo_SegredoAbaixoDeTrintaEDoisBytes_DerrubaOBoot(string ambiente)
    {
        var builder = CriarHost(ambiente, ("Agents:Hmac:SecretAtual", SegredoDeTrintaEUmBytes));

        var act = () => builder.Services.AddAgentsHmac(builder.Configuration, builder.Environment);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32 bytes*")
            .WithMessage("*openssl rand -base64 64*");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Homolog")]
    public void AddAgentsHmac_AmbienteQueExigeSegredo_SegredoDeTrintaEDoisBytes_DeixaOBootSeguir(string ambiente)
    {
        var builder = CriarHost(ambiente, ("Agents:Hmac:SecretAtual", SegredoDeTrintaEUmBytes + "2"));

        var act = () => builder.Services.AddAgentsHmac(builder.Configuration, builder.Environment);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddAgentsHmac_Development_SemSegredo_DeixaOBootSeguirComSegredoNulo()
    {
        var builder = CriarHost("Development");
        builder.Environment.EnvironmentName.Should().Be("Development");

        builder.Services.AddAgentsHmac(builder.Configuration, builder.Environment);

        using var provider = builder.Services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<AgentsHmacOptions>>().Value.SecretAtual.Should().BeNull();
    }

    [Fact]
    public void AddAgentsHmac_LigaAsDuasChavesDaSecaoAgentsHmac()
    {
        var builder = CriarHost(
            "Development",
            ("Agents:Hmac:SecretAtual", "chave-atual"),
            ("Agents:Hmac:SecretAnterior", "chave-anterior"));

        builder.Services.AddAgentsHmac(builder.Configuration, builder.Environment);

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AgentsHmacOptions>>().Value;
        options.SecretAtual.Should().Be("chave-atual");
        options.SecretAnterior.Should().Be("chave-anterior");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Homolog")]
    public void AddApiServices_AmbienteQueExigeSegredo_SemSegredoDeAgentes_DerrubaOBoot(string ambiente)
    {
        var builder = CriarHost(ambiente, ("Auth:JwtSecret", JwtSecretDeTeste));

        var act = () => builder.Services.AddApiServices(builder.Configuration, builder.Environment);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'Agents:Hmac:SecretAtual'*");
    }

    [Fact]
    public void AppSettingsVersionados_NaoCarregamSegredoDeAgentes()
    {
        var raiz = LocalizarRaizDoRepo();

        var arquivos = Directory.GetFiles(Path.Combine(raiz, "forzion.tech.Api"), "appsettings*.json");

        arquivos.Should().NotBeEmpty();
        foreach (var arquivo in arquivos)
        {
            File.ReadAllText(arquivo).Should().NotContainEquivalentOf("SecretAtual", $"{arquivo} é versionado");
            File.ReadAllText(arquivo).Should().NotContainEquivalentOf("SecretAnterior", $"{arquivo} é versionado");
        }
    }

    private static string LocalizarRaizDoRepo()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "forzion.tech.slnx")))
            diretorio = diretorio.Parent;

        diretorio.Should().NotBeNull("a suíte roda dentro do repositório");
        return diretorio!.FullName;
    }
}
