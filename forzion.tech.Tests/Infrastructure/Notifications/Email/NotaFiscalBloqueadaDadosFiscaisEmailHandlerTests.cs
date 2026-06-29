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

public class NotaFiscalBloqueadaDadosFiscaisEmailHandlerTests
{
    private readonly Mock<INotaFiscalRepository> _notaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<NotaFiscalBloqueadaDadosFiscaisEmailHandler>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly NotaFiscalBloqueadaDadosFiscaisEmailHandler _handler;

    private static readonly Guid NotaId = TestData.NextGuid();
    private static readonly Guid TreinadorId = TestData.NextGuid();
    private static readonly Guid ContaId = TestData.NextGuid();
    private static readonly Guid PagamentoId = TestData.NextGuid();

    private static NotaFiscalBloqueadaDadosFiscaisEvent Evento() =>
        new(NotaId, TreinadorId, TestData.Agora);

    private static NotaFiscal NotaBloqueada()
    {
        var nota = NotaFiscal.CriarAssinatura(TreinadorId, PagamentoId, 99.90m, TestData.Agora).Value;
        nota.MarcarBloqueadaDadosFiscais(TestData.Agora);
        return nota;
    }

    private static Treinador TreinadorValido() =>
        new TreinadorBuilder().ComContaId(ContaId).ComNome("Carlos Treinador").Build();

    private static Conta ContaValida() =>
        Conta.Criar(DomainEmail.Criar("carlos@treinador.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;

    public NotaFiscalBloqueadaDadosFiscaisEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _handler = new NotaFiscalBloqueadaDadosFiscaisEmailHandler(
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
            .ReturnsAsync(NotaBloqueada());
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
            .ReturnsAsync(NotaBloqueada());
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
    public async Task HandleAsync_DadosValidos_EnviaComLinkDadosFiscais()
    {
        _notaRepo.Setup(r => r.ObterPorIdAsync(NotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotaBloqueada());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinadorValido());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaValida());

        await _handler.HandleAsync(Evento());

        _emailService.Verify(e => e.EnviarAsync(
            "carlos@treinador.com",
            It.Is<string>(s => s.Contains("dados fiscais")),
            It.Is<string>(html => html.Contains("Carlos Treinador")
                && html.Contains("https://app.forzion.tech/treinador/dados-fiscais")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
