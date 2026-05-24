using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class AssinaturaAlunoCriadaEmailHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
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

        _handler = new AssinaturaAlunoCriadaEmailHandler(
            _alunoRepo.Object, _treinadorRepo.Object, _pacoteRepo.Object,
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
    public async Task HandleAsync_AlunoSemEmail_NaoEnvia()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "João");
        _alunoRepo.Setup(r => r.ObterPorIdAsync(Evento.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_EnviaEmailComDadosDaAssinaturaAluno()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", email: "joao@example.com");
        var treinador = Treinador.Criar(Guid.NewGuid(), "Lucas");
        var pacote = Pacote.Criar(Guid.NewGuid(), "Pacote Premium", 250.00m);

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
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", email: "maria@example.com");
        var pacote = Pacote.Criar(Guid.NewGuid(), "Básico", 150.00m);

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
        var aluno = Aluno.Criar(Guid.NewGuid(), "Pedro", email: "pedro@example.com");
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");

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
