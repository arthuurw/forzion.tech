using FluentAssertions;
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

public class PagamentoEstornadoEmailHandlerTests
{
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<PagamentoEstornadoEmailHandler>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly PagamentoEstornadoEmailHandler _handler;

    private static readonly Guid PagamentoId = Guid.NewGuid();
    private static readonly Guid AssinaturaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();

    private static readonly PagamentoEstornadoEvent Evento = new(
        PagamentoId, AssinaturaId, 149.90m, TestData.Agora);

    public PagamentoEstornadoEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _handler = new PagamentoEstornadoEmailHandler(
            _assinaturaRepo.Object, _alunoRepo.Object, _contaRepo.Object,
            _emailService.Object, _appSettings, _logger.Object);
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
    public async Task HandleAsync_AlunoNaoEncontrado_NaoEnvia()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoComEmail_UsaEmailDoAluno()
    {
        var aluno = Aluno.Criar(ContaId, "Maria", TestData.Agora, email: "maria@aluno.com");
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "maria@aluno.com",
            It.Is<string>(s => s.Contains("estornada", StringComparison.OrdinalIgnoreCase)),
            It.Is<string>(html => html.Contains("Maria") && html.Contains("149,90")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoSemEmail_FallbackContaEmail()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora);
        var conta = Conta.Criar(DomainEmail.Criar("joao@conta.com"), "hash-test-123", TipoConta.Aluno, TestData.Agora);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
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
    public async Task HandleAsync_AlunoSemEmailEContaNull_NaoEnvia()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_LinkPortalApontaParaFrontendBaseUrl()
    {
        var aluno = Aluno.Criar(ContaId, "Maria", TestData.Agora, email: "m@x.com");
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("https://app.forzion.tech/aluno/pagamentos")
                && !html.Contains("stripe.com")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
