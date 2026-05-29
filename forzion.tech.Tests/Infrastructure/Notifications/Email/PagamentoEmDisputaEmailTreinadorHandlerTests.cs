using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;
using EmailVO = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class PagamentoEmDisputaEmailTreinadorHandlerTests
{
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<PagamentoEmDisputaEmailTreinadorHandler>> _logger = new();
    private readonly PagamentoEmDisputaEmailTreinadorHandler _handler;

    private static readonly Guid PagamentoId = Guid.NewGuid();
    private static readonly Guid AssinaturaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();

    private static readonly PagamentoEmDisputaEvent Evento = new(
        PagamentoId, AssinaturaId, 149.90m, "fraudulent", TestData.Agora);

    public PagamentoEmDisputaEmailTreinadorHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new PagamentoEmDisputaEmailTreinadorHandler(
            _assinaturaRepo.Object, _treinadorRepo.Object, _contaRepo.Object, _alunoRepo.Object,
            _emailService.Object, _logger.Object);
    }

    private AssinaturaAluno AssinaturaValida() =>
        AssinaturaAluno.Criar(Guid.NewGuid(), PacoteId, TreinadorId, AlunoId, 149.90m, TestData.Agora);

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
    public async Task HandleAsync_AssinaturaNaoEncontrada_NaoEnvia()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_NaoEnvia()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaTreinadorNaoEncontrada_NaoEnvia()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", TestData.Agora);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(treinador.ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_EnviaEmailUrgenteAoTreinadorComMotivoEValor()
    {
        var conta = Conta.Criar(EmailVO.FromDatabase("carlos@treinador.com"), "hash", TipoConta.Treinador, TestData.Agora);
        var treinador = Treinador.Criar(conta.Id, "Carlos", TestData.Agora);
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(treinador.ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "carlos@treinador.com",
            It.Is<string>(s => s.Contains("URGENTE", StringComparison.OrdinalIgnoreCase)
                && s.Contains("Disputa", StringComparison.OrdinalIgnoreCase)),
            It.Is<string>(html => html.Contains("Carlos")
                && html.Contains("Maria")
                && html.Contains("149,90")
                && html.Contains("Fraude")
                && html.Contains("dashboard.stripe.com")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_UsaPlaceholderEContinuaEnviando()
    {
        // Aluno deletado/inativo não impede notificar o treinador — disputa é urgente.
        var conta = Conta.Criar(EmailVO.FromDatabase("carlos@x.com"), "hash", TipoConta.Treinador, TestData.Agora);
        var treinador = Treinador.Criar(conta.Id, "Carlos", TestData.Agora);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(treinador.ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "carlos@x.com",
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("aluno")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
