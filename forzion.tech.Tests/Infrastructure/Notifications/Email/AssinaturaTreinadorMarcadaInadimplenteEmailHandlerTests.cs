using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class AssinaturaTreinadorMarcadaInadimplenteEmailHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<AssinaturaTreinadorMarcadaInadimplenteEmailHandler>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly AssinaturaTreinadorMarcadaInadimplenteEmailHandler _handler;

    private static readonly Guid AssinaturaId = TestData.NextGuid();
    private static readonly Guid TreinadorId = TestData.NextGuid();
    private static readonly Guid ContaId = TestData.NextGuid();

    private static readonly AssinaturaTreinadorMarcadaInadimplenteEvent Evento =
        new(AssinaturaId, TreinadorId, 3, TestData.Agora);

    private static Treinador TreinadorValido() =>
        new TreinadorBuilder().ComContaId(ContaId).ComNome("Ana Treinadora").Build();

    private static Conta ContaValida() =>
        Conta.Criar(DomainEmail.Criar("ana@treinadora.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;

    public AssinaturaTreinadorMarcadaInadimplenteEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _handler = new AssinaturaTreinadorMarcadaInadimplenteEmailHandler(
            _treinadorRepo.Object, _contaRepo.Object,
            _emailService.Object, _appSettings, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_NaoEnvia()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_NaoEnvia()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaNaoEncontrada_NaoEnvia()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinadorValido());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_EnviaParaContaDoTreinador()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinadorValido());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaValida());

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "ana@treinadora.com",
            It.Is<string>(s => s.Contains("inadimplência")),
            It.Is<string>(html => html.Contains("Ana Treinadora") && html.Contains("Regularizar")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_LinkPortalApontaParaFrontendBaseUrl()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinadorValido());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaValida());

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("https://app.forzion.tech/treinador/plano")
                && !html.Contains("stripe.com")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
