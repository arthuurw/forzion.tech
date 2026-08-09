using forzion.tech.Api.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Sentry;
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
    public void AddSentryLogging_DsnPresente_NaoLanca()
    {
        var services = new ServiceCollection();

        var act = () => services.AddSentryLogging(CriarConfig(dsn: "https://key@o0.ingest.sentry.io/0"), CriarEnv());

        act.Should().NotThrow("Sentry nunca deve derrubar o boot da app, mesmo com DSN sintaticamente estranho");
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
