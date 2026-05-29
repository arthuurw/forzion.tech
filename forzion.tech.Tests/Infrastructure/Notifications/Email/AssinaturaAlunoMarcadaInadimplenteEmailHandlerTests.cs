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

public class AssinaturaAlunoMarcadaInadimplenteEmailHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly Mock<ILogger<AssinaturaAlunoMarcadaInadimplenteEmailHandler>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly AssinaturaAlunoMarcadaInadimplenteEmailHandler _handler;

    private static readonly Guid AssinaturaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static readonly AssinaturaAlunoMarcadaInadimplenteEvent Evento =
        new(AssinaturaId, AlunoId, TreinadorId, 3, TestData.Agora);

    public AssinaturaAlunoMarcadaInadimplenteEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);
        _planoPolicy.Setup(p => p.ResolverPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, true));

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _handler = new AssinaturaAlunoMarcadaInadimplenteEmailHandler(
            _alunoRepo.Object, _contaRepo.Object,
            _emailService.Object, _appSettings, _planoPolicy.Object, _logger.Object);
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
    public async Task HandleAsync_AlunoComEmail_UsaEmailDoAluno()
    {
        var aluno = Aluno.Criar(ContaId, "Maria", TestData.Agora, email: "maria@aluno.com").Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "maria@aluno.com",
            It.Is<string>(s => s.Contains("inadimplência")),
            It.Is<string>(html => html.Contains("Maria") && html.Contains("Regularizar")),
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
    public async Task HandleAsync_AlunoSemEmailEContaNull_NaoEnvia()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_LinkPortalApontaParaFrontendBaseUrl()
    {
        var aluno = Aluno.Criar(ContaId, "Maria", TestData.Agora, email: "m@x.com").Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
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

    [Fact]
    public async Task HandleAsync_PlanoSemPermissaoEmail_NaoEnvia()
    {
        _planoPolicy.Setup(p => p.ResolverPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(false, false));

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
