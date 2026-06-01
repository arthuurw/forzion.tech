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

public class VinculoPendenteCriadoEmailTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly Mock<ILogger<VinculoPendenteCriadoEmailTreinadorHandler>> _logger = new();
    private readonly VinculoPendenteCriadoEmailTreinadorHandler _handler;

    private static readonly Guid VinculoId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid ContaTreinadorId = Guid.NewGuid();

    private static readonly VinculoPendenteCriadoEvent Evento =
        new(VinculoId, TreinadorId, AlunoId, TestData.Agora);

    public VinculoPendenteCriadoEmailTreinadorHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, true));

        _handler = new VinculoPendenteCriadoEmailTreinadorHandler(
            _treinadorRepo.Object, _contaRepo.Object, _alunoRepo.Object,
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
    public async Task HandleAsync_TreinadorSemConta_NaoEnvia()
    {
        var treinador = Treinador.Criar(ContaTreinadorId, "Carlos", TestData.Agora).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaTreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorComContaEAluno_EnviaParaEmailDaContaComAssuntoEConteudoCorretos()
    {
        var treinador = Treinador.Criar(ContaTreinadorId, "Coach Carlos", TestData.Agora).Value;
        var conta = Conta.Criar(DomainEmail.Criar("carlos@coach.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;
        var aluno = Aluno.Criar(Guid.NewGuid(), "Pedro", TestData.Agora).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaTreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "carlos@coach.com",
            It.Is<string>(s => s.Contains("aprovação", StringComparison.OrdinalIgnoreCase)),
            It.Is<string>(html =>
                html.Contains("Coach Carlos") &&
                html.Contains("Pedro")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_UsaPlaceholder()
    {
        var treinador = Treinador.Criar(ContaTreinadorId, "Coach", TestData.Agora).Value;
        var conta = Conta.Criar(DomainEmail.Criar("c@x.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaTreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("Um aluno")),
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
