using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class EnvironmentEmailDecoratorTests
{
    private readonly Mock<IEmailService> _inner = new();
    private readonly Mock<ILogger<EnvironmentEmailDecorator>> _logger = new();

    private sealed record Enviado(string Para, string Assunto, string Html);

    private (EnvironmentEmailDecorator decorator, Func<Enviado?> capturado) Build(EmailSettings settings)
    {
        Enviado? capturado = null;
        _inner
            .Setup(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((para, assunto, html, _) => capturado = new Enviado(para, assunto, html))
            .Returns(Task.CompletedTask);

        var decorator = new EnvironmentEmailDecorator(_inner.Object, settings, _logger.Object);
        return (decorator, () => capturado);
    }

    [Fact]
    public async Task Prod_Passthrough_NaoAlteraNada()
    {
        var (decorator, capturado) = Build(new EmailSettings
        {
            MarcarComoTeste = false,
            PrefixoAssuntoTeste = "[HOMOLOG - TESTE]",
            RedirecionarDestinatariosPara = "qa@forzion.tech"
        });

        await decorator.EnviarAsync("real@cliente.com", "Bem-vindo", "<p>corpo</p>");

        var e = capturado()!;
        e.Para.Should().Be("real@cliente.com");
        e.Assunto.Should().Be("Bem-vindo");
        e.Html.Should().Be("<p>corpo</p>");
    }

    [Fact]
    public async Task NaoProd_PrefixaAssunto()
    {
        var (decorator, capturado) = Build(new EmailSettings
        {
            MarcarComoTeste = true,
            PrefixoAssuntoTeste = "[HOMOLOG - TESTE]"
        });

        await decorator.EnviarAsync("dest@forzion.tech", "Bem-vindo", "<p>corpo</p>");

        capturado()!.Assunto.Should().Be("[HOMOLOG - TESTE] Bem-vindo");
    }

    [Fact]
    public async Task NaoProd_PrefixoVazio_NaoAlteraAssunto()
    {
        var (decorator, capturado) = Build(new EmailSettings
        {
            MarcarComoTeste = true,
            PrefixoAssuntoTeste = ""
        });

        await decorator.EnviarAsync("dest@forzion.tech", "Bem-vindo", "<p>corpo</p>");

        capturado()!.Assunto.Should().Be("Bem-vindo");
    }

    [Fact]
    public async Task NaoProd_InjetaBannerPreservandoCorpo()
    {
        var (decorator, capturado) = Build(new EmailSettings { MarcarComoTeste = true });

        await decorator.EnviarAsync("dest@forzion.tech", "Assunto", "<p>corpo original</p>");

        var html = capturado()!.Html;
        html.Should().Contain("E-mail de teste");
        html.Should().Contain("<p>corpo original</p>");
    }

    [Fact]
    public async Task NaoProd_ComRedirect_ForaDaAllowlist_RedirecionaDestinatario()
    {
        var (decorator, capturado) = Build(new EmailSettings
        {
            MarcarComoTeste = true,
            RedirecionarDestinatariosPara = "qa@forzion.tech",
            AllowlistDominios = "forzion.tech"
        });

        await decorator.EnviarAsync("real@cliente.com", "Assunto", "<p>corpo</p>");

        capturado()!.Para.Should().Be("qa@forzion.tech");
    }

    [Fact]
    public async Task NaoProd_DestinatarioNaAllowlist_PassaDireto()
    {
        var (decorator, capturado) = Build(new EmailSettings
        {
            MarcarComoTeste = true,
            RedirecionarDestinatariosPara = "qa@forzion.tech",
            AllowlistDominios = "forzion.tech"
        });

        await decorator.EnviarAsync("interno@forzion.tech", "Assunto", "<p>corpo</p>");

        capturado()!.Para.Should().Be("interno@forzion.tech");
    }

    [Fact]
    public async Task NaoProd_SemAlvoDeRedirect_MantemDestinatario()
    {
        var (decorator, capturado) = Build(new EmailSettings
        {
            MarcarComoTeste = true,
            RedirecionarDestinatariosPara = ""
        });

        await decorator.EnviarAsync("real@cliente.com", "Assunto", "<p>corpo</p>");

        capturado()!.Para.Should().Be("real@cliente.com");
    }

    [Fact]
    public void Habilitado_DelegaParaInner()
    {
        _inner.SetupGet(s => s.Habilitado).Returns(true);
        var (decorator, _) = Build(new EmailSettings());

        decorator.Habilitado.Should().BeTrue();
        _inner.VerifyGet(s => s.Habilitado, Times.Once);
    }
}
