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

public class AssinaturaTreinadorPagamentoFalhouEmailHandlerTests
{
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<AssinaturaTreinadorPagamentoFalhouEmailHandler>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly AssinaturaTreinadorPagamentoFalhouEmailHandler _handler;

    private static readonly Guid AssinaturaId = TestData.NextGuid();
    private static readonly Guid TreinadorId = TestData.NextGuid();
    private static readonly Guid ContaId = TestData.NextGuid();
    private static readonly Guid PlanoId = TestData.NextGuid();

    private static AssinaturaTreinadorPagamentoFalhouEvent EventoCom(int tentativas) =>
        new(AssinaturaId, TreinadorId, tentativas, TestData.Agora);

    private static AssinaturaTreinador AssinaturaValida() =>
        AssinaturaTreinador.Criar(TreinadorId, PlanoId, 99.90m, TestData.Agora).Value;

    private static Treinador TreinadorValido() =>
        new TreinadorBuilder().ComContaId(ContaId).ComNome("Carlos Treinador").Build();

    private static Conta ContaValida() =>
        Conta.Criar(DomainEmail.Criar("carlos@treinador.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;

    public AssinaturaTreinadorPagamentoFalhouEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _handler = new AssinaturaTreinadorPagamentoFalhouEmailHandler(
            _assinaturaRepo.Object, _treinadorRepo.Object, _contaRepo.Object,
            _emailService.Object, _appSettings, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_NaoEnvia()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);

        await _handler.HandleAsync(EventoCom(1));

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_NaoEnvia()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        await _handler.HandleAsync(EventoCom(1));

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

        await _handler.HandleAsync(EventoCom(1));

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaNaoEncontrada_NaoEnvia()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinadorValido());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaValida());
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaTreinador?)null);

        await _handler.HandleAsync(EventoCom(1));

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task HandleAsync_DadosValidos_EnviaParaContaDoTreinador(int tentativas)
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinadorValido());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaValida());
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());

        await _handler.HandleAsync(EventoCom(tentativas));

        _emailService.Verify(e => e.EnviarAsync(
            "carlos@treinador.com",
            It.Is<string>(s => s.Contains("Cobrança")),
            It.Is<string>(html => html.Contains("Carlos Treinador") && html.Contains("99,90")),
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
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());

        await _handler.HandleAsync(EventoCom(1));

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("https://app.forzion.tech/treinador/plano")
                && !html.Contains("stripe.com")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
