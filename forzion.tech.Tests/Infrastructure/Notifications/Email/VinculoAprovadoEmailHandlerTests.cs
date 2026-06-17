using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class VinculoAprovadoEmailHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly Mock<ILogger<VinculoAprovadoEmailHandler>> _logger = new();
    private readonly VinculoAprovadoEmailHandler _handler;

    private static readonly VinculoAprovadoEvent Evento = new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

    public VinculoAprovadoEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);
        _planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, true));

        _handler = new VinculoAprovadoEmailHandler(
            _alunoRepo.Object, _contaRepo.Object, _treinadorRepo.Object, _emailService.Object,
            _planoPolicy.Object, _logger.Object);
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
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoSemEmailEContaNaoEncontrada_NaoEnvia()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", DateTime.UtcNow).Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(Evento.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        // _contaRepo retorna null por padrão (setup no construtor)

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoSemEmail_UsaEmailDaConta()
    {
        var contaId = Guid.NewGuid();
        var aluno = Aluno.Criar(contaId, "João", DateTime.UtcNow).Value;
        var conta = new ContaBuilder().ComEmail("joao@conta.com").Build();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Lucas", DateTime.UtcNow).Value;

        _alunoRepo.Setup(r => r.ObterPorIdAsync(Evento.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "joao@conta.com",
            "Vínculo aprovado — forzion.tech",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_EnviaEmailComNomeTreinador()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", DateTime.UtcNow, email: "joao@example.com").Value;
        var treinador = Treinador.Criar(Guid.NewGuid(), "Lucas", DateTime.UtcNow).Value;

        _alunoRepo.Setup(r => r.ObterPorIdAsync(Evento.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "joao@example.com",
            "Vínculo aprovado — forzion.tech",
            It.Is<string>(html => html.Contains("João") && html.Contains("Lucas")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_UsaFallback()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", DateTime.UtcNow, email: "maria@example.com").Value;

        _alunoRepo.Setup(r => r.ObterPorIdAsync(Evento.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "maria@example.com",
            "Vínculo aprovado — forzion.tech",
            It.Is<string>(html => html.Contains("Quem te treina")),
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
