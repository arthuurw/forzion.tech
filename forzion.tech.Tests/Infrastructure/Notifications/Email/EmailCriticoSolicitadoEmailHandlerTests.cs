using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class EmailCriticoSolicitadoEmailHandlerTests
{
    private const string Destino = "user@example.com";
    private const string BaseUrl = "https://app.forzion.tech";

    private readonly IDataProtectionProvider _dataProtection =
        new ServiceCollection().AddDataProtection().Services.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly EmailCriticoDispatcher _dispatcher;
    private EmailCriticoSolicitadoEvent _capturado = null!;

    public EmailCriticoSolicitadoEmailHandlerTests()
    {
        var outbox = new Mock<IOutboxEnfileirador>();
        outbox.Setup(o => o.Enfileirar(It.IsAny<string>(), It.IsAny<EmailCriticoSolicitadoEvent>(), It.IsAny<string>()))
            .Callback<string, EmailCriticoSolicitadoEvent, string>((_, e, _) => _capturado = e);
        _dispatcher = new EmailCriticoDispatcher(outbox.Object, _dataProtection,
            new FakeTimeProvider(new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero)));
        _emailService.SetupGet(s => s.Habilitado).Returns(true);
    }

    private EmailCriticoSolicitadoEmailHandler BuildHandler(
        string environmentName = "Production",
        ILogger<EmailCriticoSolicitadoEmailHandler>? logger = null)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return new(_emailService.Object, _dataProtection,
            Options.Create(new AppSettings { FrontendBaseUrl = BaseUrl }),
            environment.Object,
            logger ?? NullLogger<EmailCriticoSolicitadoEmailHandler>.Instance);
    }

    private EmailCriticoSolicitadoEvent Evento(EmailCriticoTemplate template, string segredo)
    {
        _dispatcher.Enfileirar(template, Destino, segredo);
        return _capturado;
    }

    [Fact]
    public async Task HandleAsync_VerificarEmail_EnviaComLinkContendoToken()
    {
        string? assunto = null, corpo = null, para = null;
        _emailService.Setup(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, string, string, CancellationToken, string?>((p, a, c, _, _) => (para, assunto, corpo) = (p, a, c))
            .Returns(Task.CompletedTask);

        await BuildHandler().HandleAsync(Evento(EmailCriticoTemplate.VerificarEmail, "tok-verif"));

        para.Should().Be(Destino);
        assunto.Should().Contain("Confirme");
        corpo.Should().Contain($"{BaseUrl}/verify-email?token=tok-verif");
    }

    [Fact]
    public async Task HandleAsync_RedefinirSenha_EnviaComLinkDeReset()
    {
        string? corpo = null;
        _emailService.Setup(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, string, string, CancellationToken, string?>((_, _, c, _, _) => corpo = c)
            .Returns(Task.CompletedTask);

        await BuildHandler().HandleAsync(Evento(EmailCriticoTemplate.RedefinirSenha, "tok-reset"));

        corpo.Should().Contain($"{BaseUrl}/reset-password?token=tok-reset");
    }

    [Fact]
    public async Task HandleAsync_CodigoMfa_EnviaComCodigoNoCorpo()
    {
        string? corpo = null;
        _emailService.Setup(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, string, string, CancellationToken, string?>((_, _, c, _, _) => corpo = c)
            .Returns(Task.CompletedTask);

        await BuildHandler().HandleAsync(Evento(EmailCriticoTemplate.CodigoMfa, "123456"));

        corpo.Should().Contain("123456");
    }

    [Fact]
    public async Task HandleAsync_TrocaEmail_EnviaComCodigoNoCorpo()
    {
        string? corpo = null;
        _emailService.Setup(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, string, string, CancellationToken, string?>((_, _, c, _, _) => corpo = c)
            .Returns(Task.CompletedTask);

        await BuildHandler().HandleAsync(Evento(EmailCriticoTemplate.TrocaEmail, "654321"));

        corpo.Should().Contain("654321");
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_NaoEnvia()
    {
        _emailService.SetupGet(s => s.Habilitado).Returns(false);

        await BuildHandler().HandleAsync(Evento(EmailCriticoTemplate.CodigoMfa, "123456"));

        _emailService.Verify(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitadoForaDeDevelopment_NaoLogaSegredo()
    {
        _emailService.SetupGet(s => s.Habilitado).Returns(false);
        var coletor = new ColetorLog<EmailCriticoSolicitadoEmailHandler>();

        await BuildHandler("Homolog", coletor).HandleAsync(Evento(EmailCriticoTemplate.CodigoMfa, "123456"));

        coletor.Mensagens.Should().NotContain(m => m.Contains("123456"));
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitadoEmDevelopment_LogaSegredoParaDebug()
    {
        _emailService.SetupGet(s => s.Habilitado).Returns(false);
        var coletor = new ColetorLog<EmailCriticoSolicitadoEmailHandler>();

        await BuildHandler("Development", coletor).HandleAsync(Evento(EmailCriticoTemplate.CodigoMfa, "123456"));

        coletor.Mensagens.Should().Contain(m => m.Contains("123456"));
    }

    [Fact]
    public async Task HandleAsync_EnvioFalha_PropagaExcecaoParaRetry()
    {
        _emailService.Setup(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("falha smtp"));

        var act = async () => await BuildHandler().HandleAsync(Evento(EmailCriticoTemplate.VerificarEmail, "tok"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleAsync_CargaCifradaAdulterada_Lanca()
    {
        var original = Evento(EmailCriticoTemplate.VerificarEmail, "tok");
        var adulterado = original with { DadosCifrados = original.DadosCifrados[..^4] + "AAAA" };

        var act = async () => await BuildHandler().HandleAsync(adulterado);

        await act.Should().ThrowAsync<Exception>();
    }

    private sealed class ColetorLog<T> : ILogger<T>
    {
        public List<string> Mensagens { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Mensagens.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
