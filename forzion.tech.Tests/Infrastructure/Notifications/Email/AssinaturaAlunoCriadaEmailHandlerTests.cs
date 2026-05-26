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

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class AssinaturaAlunoCriadaEmailHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<AssinaturaAlunoCriadaEmailHandler>> _logger = new();
    private readonly AssinaturaAlunoCriadaEmailHandler _handler;

    private static readonly AssinaturaAlunoCriadaEvent Evento = new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 250.00m, DateTime.UtcNow);

    public AssinaturaAlunoCriadaEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        _handler = new AssinaturaAlunoCriadaEmailHandler(
            _alunoRepo.Object, _contaRepo.Object, _treinadorRepo.Object, _pacoteRepo.Object,
            _emailService.Object, _logger.Object);
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
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", DateTime.UtcNow);
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
        var aluno = Aluno.Criar(contaId, "João", DateTime.UtcNow);
        var conta = new ContaBuilder().ComEmail("joao@conta.com").Build();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Lucas", DateTime.UtcNow);
        var pacote = Pacote.Criar(Guid.NewGuid(), "Premium", 250m, DateTime.UtcNow);

        _alunoRepo.Setup(r => r.ObterPorIdAsync(Evento.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(Evento.PacoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pacote);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "joao@conta.com",
            "AssinaturaAluno criada — forzion.tech",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_EnviaEmailComDadosDaAssinaturaAluno()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", DateTime.UtcNow, email: "joao@example.com");
        var treinador = Treinador.Criar(Guid.NewGuid(), "Lucas", DateTime.UtcNow);
        var pacote = Pacote.Criar(Guid.NewGuid(), "Pacote Premium", 250.00m, DateTime.UtcNow);

        _alunoRepo.Setup(r => r.ObterPorIdAsync(Evento.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(Evento.PacoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pacote);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "joao@example.com",
            "AssinaturaAluno criada — forzion.tech",
            It.Is<string>(html =>
                html.Contains("João") &&
                html.Contains("Lucas") &&
                html.Contains("Pacote Premium") &&
                html.Contains("250")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_UsaFallback()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", DateTime.UtcNow, email: "maria@example.com");
        var pacote = Pacote.Criar(Guid.NewGuid(), "Básico", 150.00m, DateTime.UtcNow);

        _alunoRepo.Setup(r => r.ObterPorIdAsync(Evento.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(Evento.PacoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pacote);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "maria@example.com",
            "AssinaturaAluno criada — forzion.tech",
            It.Is<string>(html => html.Contains("seu treinador")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PacoteNaoEncontrado_UsaFallback()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Pedro", DateTime.UtcNow, email: "pedro@example.com");
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow);

        _alunoRepo.Setup(r => r.ObterPorIdAsync(Evento.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(Evento.PacoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pacote?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "pedro@example.com",
            "AssinaturaAluno criada — forzion.tech",
            It.Is<string>(html => html.Contains("Padrão")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
