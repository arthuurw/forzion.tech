using forzion.tech.Api.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Sentry;
using Sentry.Extensions.Logging;
using Sentry.Protocol;

namespace forzion.tech.Tests.Api.Extensions;

public class DependencyInjectionExtensionsSentryTests
{
    private static IWebHostEnvironment CriarEnv(string name = "Production") =>
        Mock.Of<IWebHostEnvironment>(e => e.EnvironmentName == name);

    private static IConfiguration CriarConfig(string? dsn) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(dsn is null ? [] : new Dictionary<string, string?> { ["Sentry:Dsn"] = dsn })
            .Build();

    [Fact]
    public void AddSentryLogging_DsnAusente_NaoRegistraILoggerProvider()
    {
        var services = new ServiceCollection();

        services.AddSentryLogging(CriarConfig(dsn: null), CriarEnv());

        using var provider = services.BuildServiceProvider();
        provider.GetServices<ILoggerProvider>().Should().BeEmpty("sem DSN o sink deve ficar em no-op silencioso");
    }

    [Fact]
    public void AddSentryLogging_DsnEmBranco_NaoRegistraILoggerProvider()
    {
        var services = new ServiceCollection();

        services.AddSentryLogging(CriarConfig(dsn: "   "), CriarEnv());

        using var provider = services.BuildServiceProvider();
        provider.GetServices<ILoggerProvider>().Should().BeEmpty();
    }

    [Fact]
    public void AddSentryLogging_DsnValido_RegistraILoggerProviderCoexistindoComOutros()
    {
        var services = new ServiceCollection();
        var dbSinkFalso = Mock.Of<ILoggerProvider>();
        services.AddSingleton(dbSinkFalso);

        services.AddSentryLogging(CriarConfig(dsn: "https://key@o0.ingest.sentry.io/0"), CriarEnv());

        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetServices<ILoggerProvider>().ToList();

        act.Should().NotThrow("resolver o provider é onde Sentry.Dsn.Parse lançaria pra um DSN malformado");
        var providers = act();
        providers.Should().Contain(dbSinkFalso, "o sink de DB já registrado não pode ser substituído pelo de Sentry");
        providers.Should().Contain(p => p.GetType().Namespace!.StartsWith("Sentry", StringComparison.Ordinal),
            "AddSentry deve registrar pelo menos um ILoggerProvider próprio");
    }

    [Theory]
    [InlineData("isto-nao-e-uma-url-valida")]
    [InlineData("https://key@o0.ingest.sentry.io")]
    [InlineData("https://:secret@o0.ingest.sentry.io/1")]
    public void AddSentryLogging_DsnMalformado_NaoRegistraILoggerProviderENaoLanca(string dsn)
    {
        var services = new ServiceCollection();

        services.AddSentryLogging(CriarConfig(dsn), CriarEnv());

        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetServices<ILoggerProvider>().ToList();

        act.Should().NotThrow("resolver o provider é onde Sentry.Dsn.Parse lançaria pra um DSN malformado");
        act().Should().BeEmpty("DSN sintaticamente inválido deve cair no mesmo no-op do DSN ausente");
    }

    [Fact]
    public void AddSentryLogging_DsnValido_ConfiguraOpcoesDePii()
    {
        var services = new ServiceCollection();

        services.AddSentryLogging(CriarConfig(dsn: "https://key@o0.ingest.sentry.io/0"), CriarEnv());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SentryLoggingOptions>>().Value;

        options.SendDefaultPii.Should().BeFalse("SendDefaultPii deve ficar explicitamente false, não depender do default do SDK");
        options.MinimumEventLevel.Should().Be(LogLevel.Error, "mesmo teto do sink de DB — Warning/Info não devem virar evento Sentry");
        options.MinimumBreadcrumbLevel.Should().Be(LogLevel.None, "breadcrumb desligado — nenhum log intermediário deve sair como breadcrumb");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("isto-nao-e-uma-url-valida")]
    [InlineData("ftp://key@o0.ingest.sentry.io/0")]
    [InlineData("https://key@o0.ingest.sentry.io")] // sem project id — Sentry.Dsn.Parse lança
    [InlineData("https://key@o0.ingest.sentry.io/")]
    [InlineData("https://:secret@o0.ingest.sentry.io/1")] // public key vazia antes do ':'
    public void DsnValido_DsnAusenteVazioOuMalformado_RetornaFalse(string? dsn)
    {
        DependencyInjectionExtensions.DsnValido(dsn).Should().BeFalse();
    }

    [Fact]
    public void DsnValido_DsnBemFormado_RetornaTrue()
    {
        DependencyInjectionExtensions.DsnValido("https://key@o0.ingest.sentry.io/0").Should().BeTrue();
    }

    [Fact]
    public void ScrubPii_MensagemComEmailETelefone_MascaraAntesDeSair()
    {
        var evento = new SentryEvent
        {
            Message = new SentryMessage { Formatted = "falha para user@example.com fone 11987654321" }
        };

        var resultado = DependencyInjectionExtensions.ScrubPii(evento);

        resultado.Message!.Formatted.Should().NotContain("user@example.com");
        resultado.Message.Formatted.Should().NotContain("11987654321");
        resultado.Message.Formatted.Should().Contain("[email]");
        resultado.Message.Formatted.Should().Contain("[num]");
    }

    [Fact]
    public void ScrubPii_MensagemComTemplate_PreservaMessageParaAgrupamentoDoSentry()
    {
        var evento = new SentryEvent
        {
            Message = new SentryMessage { Message = "falha para {Email}", Formatted = "falha para user@example.com" }
        };

        var resultado = DependencyInjectionExtensions.ScrubPii(evento);

        resultado.Message!.Message.Should().Be("falha para {Email}");
    }

    [Fact]
    public void ScrubPii_TagComEmail_MascaraAntesDeSair()
    {
        var evento = new SentryEvent();
        evento.SetTag("Email", "user@example.com");

        var resultado = DependencyInjectionExtensions.ScrubPii(evento);

        resultado.Tags["Email"].Should().NotContain("user@example.com");
        resultado.Tags["Email"].Should().Contain("[email]");
    }

    [Fact]
    public void ScrubPii_TextoDeExcecaoComEmail_MascaraAntesDeSair()
    {
        var evento = new SentryEvent
        {
            SentryExceptions = [new SentryException { Value = "falha para user@example.com" }]
        };

        var resultado = DependencyInjectionExtensions.ScrubPii(evento);

        resultado.SentryExceptions.Should().NotBeNullOrEmpty();
        resultado.SentryExceptions!.Should().OnlyContain(e => e.Value != null && !e.Value.Contains("user@example.com"));
        resultado.SentryExceptions!.Should().OnlyContain(e => e.Value != null && e.Value.Contains("[email]"));
    }

    [Fact]
    public void ScrubPii_SemMensagemNemExcecao_NaoLanca()
    {
        var evento = new SentryEvent();

        var act = () => DependencyInjectionExtensions.ScrubPii(evento);

        act.Should().NotThrow();
    }
}
