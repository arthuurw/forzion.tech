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

public class NfseEmitidaEmailHandlerTests
{
    private readonly Mock<INotaFiscalRepository> _notaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<NfseEmitidaEmailHandler>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly NfseEmitidaEmailHandler _handler;

    private static readonly Guid NotaId = TestData.NextGuid();
    private static readonly Guid TreinadorId = TestData.NextGuid();
    private static readonly Guid ContaId = TestData.NextGuid();
    private static readonly Guid PagamentoId = TestData.NextGuid();

    private static NotaFiscalEmitidaEvent Evento() =>
        new(NotaId, TreinadorId, "CHV-EVT", TestData.Agora);

    private static NotaFiscal NotaEmitida()
    {
        var nota = NotaFiscal.CriarAssinatura(TreinadorId, PagamentoId, 50m, TestData.Agora).Value;
        nota.MarcarEmitida("CHV-123", "2025/000042", TestData.Agora, "danfse-ref", TestData.Agora);
        return nota;
    }

    private static Treinador TreinadorValido() =>
        new TreinadorBuilder().ComContaId(ContaId).ComNome("Carlos Treinador").Build();

    private static Conta ContaValida() =>
        Conta.Criar(DomainEmail.Criar("carlos@treinador.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;

    public NfseEmitidaEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _handler = new NfseEmitidaEmailHandler(
            _notaRepo.Object, _treinadorRepo.Object, _contaRepo.Object,
            _emailService.Object, _appSettings, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_NaoEnvia()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);

        await _handler.HandleAsync(Evento());

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NotaNaoEncontrada_NaoEnvia()
    {
        _notaRepo.Setup(r => r.ObterPorIdAsync(NotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotaFiscal?)null);

        await _handler.HandleAsync(Evento());

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_NaoEnvia()
    {
        _notaRepo.Setup(r => r.ObterPorIdAsync(NotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotaEmitida());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        await _handler.HandleAsync(Evento());

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaNaoEncontrada_NaoEnvia()
    {
        _notaRepo.Setup(r => r.ObterPorIdAsync(NotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotaEmitida());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinadorValido());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(Evento());

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_EnviaParaContaDoTreinador()
    {
        _notaRepo.Setup(r => r.ObterPorIdAsync(NotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotaEmitida());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinadorValido());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaValida());

        await _handler.HandleAsync(Evento());

        _emailService.Verify(e => e.EnviarAsync(
            "carlos@treinador.com",
            It.Is<string>(s => s.Contains("Nota fiscal")),
            It.Is<string>(html => html.Contains("Carlos Treinador")
                && html.Contains("2025/000042")
                && html.Contains("50,00")
                && html.Contains("https://app.forzion.tech/treinador/notas-fiscais")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
