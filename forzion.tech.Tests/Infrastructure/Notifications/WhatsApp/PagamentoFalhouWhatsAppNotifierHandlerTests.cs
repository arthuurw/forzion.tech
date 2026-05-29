using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.WhatsApp;

public class PagamentoFalhouWhatsAppNotifierHandlerTests
{
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IWhatsAppNotifier> _notifier = new();
    private readonly Mock<ILogger<PagamentoFalhouWhatsAppNotifierHandler>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly PagamentoFalhouWhatsAppNotifierHandler _handler;

    private static readonly Guid AssinaturaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static PagamentoFalhouEvent EventoCom(int tentativas) =>
        new(AssinaturaId, AlunoId, tentativas, TestData.Agora);

    public PagamentoFalhouWhatsAppNotifierHandlerTests()
    {
        _notifier.Setup(n => n.Habilitado).Returns(true);
        _notifier.Setup(n => n.SendTemplateAsync(It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _handler = new PagamentoFalhouWhatsAppNotifierHandler(
            _assinaturaRepo.Object, _alunoRepo.Object,
            _notifier.Object, _appSettings, _logger.Object);
    }

    private AssinaturaAluno AssinaturaValida() =>
        AssinaturaAluno.Criar(Guid.NewGuid(), PacoteId, TreinadorId, AlunoId, 149.90m, TestData.Agora).Value;

    [Fact]
    public async Task HandleAsync_HabilitadoFalso_NaoEnvia()
    {
        _notifier.Setup(n => n.Habilitado).Returns(false);

        await _handler.HandleAsync(EventoCom(1));

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PrimeiraTentativaComTelefone_EnviaTemplate()
    {
        // Parity com e-mail: WhatsApp agora dispara em TODAS as tentativas.
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, telefone: "11999998888").Value;
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(EventoCom(1));

        _notifier.Verify(n => n.SendTemplateAsync(
            "11999998888",
            It.Is<WhatsAppTemplateMessage>(m => m.Name == "cobranca_falhou"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaNaoEncontrada_NaoEnvia()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        await _handler.HandleAsync(EventoCom(2));

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_NaoEnvia()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(EventoCom(2));

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoSemTelefone_NaoEnvia()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora).Value;
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(EventoCom(2));

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SegundaTentativaComTelefone_EnviaTemplateCobrancaFalhou()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, telefone: "11999998888").Value;
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(EventoCom(2));

        _notifier.Verify(n => n.SendTemplateAsync(
            "11999998888",
            It.Is<WhatsAppTemplateMessage>(m =>
                m.Name == "cobranca_falhou" &&
                m.BodyParameters.Contains("Maria") &&
                m.BodyParameters.Contains("149,90") &&
                m.BodyParameters.Contains("2") &&
                m.BodyParameters.Any(p => p.Contains("https://app.forzion.tech/aluno/pagamentos"))),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
