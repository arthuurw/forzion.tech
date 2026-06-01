using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class AssinaturaAlunoCanceladaEmailAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly Mock<ILogger<AssinaturaAlunoCanceladaEmailAlunoHandler>> _logger = new();
    private readonly AssinaturaAlunoCanceladaEmailAlunoHandler _handler;

    private static readonly Guid AssinaturaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static readonly AssinaturaAlunoCanceladaEvent Evento =
        new(AssinaturaId, AlunoId, TreinadorId, 150m, TestData.Agora);

    public AssinaturaAlunoCanceladaEmailAlunoHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);
        _planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, true));

        _handler = new AssinaturaAlunoCanceladaEmailAlunoHandler(
            _alunoRepo.Object, _contaRepo.Object, _treinadorRepo.Object,
            _emailService.Object, _planoPolicy.Object, _logger.Object);
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
    public async Task HandleAsync_AlunoNaoEncontrado_NaoEnvia()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoComEmail_EnviaConfirmacaoCancelamento()
    {
        var aluno = Aluno.Criar(ContaId, "Maria", TestData.Agora, email: "maria@aluno.com").Value;
        var treinador = Treinador.Criar(Guid.NewGuid(), "Coach Joana", TestData.Agora).Value;

        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "maria@aluno.com",
            It.Is<string>(s => s.Contains("cancelada", StringComparison.OrdinalIgnoreCase)),
            It.Is<string>(html => html.Contains("Maria") && html.Contains("Coach Joana") && html.Contains("cancelada")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoSemEmail_FallbackContaEmail()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        var conta = Conta.Criar(DomainEmail.Criar("joao@conta.com").Value, "hash-test-123", TipoConta.Aluno, TestData.Agora).Value;

        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "joao@conta.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_UsaPlaceholder()
    {
        var aluno = Aluno.Criar(ContaId, "Ana", TestData.Agora, email: "ana@x.com").Value;

        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("seu treinador")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PlanoSemPermissaoEmail_NaoEnvia()
    {
        _planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(false, false));

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
