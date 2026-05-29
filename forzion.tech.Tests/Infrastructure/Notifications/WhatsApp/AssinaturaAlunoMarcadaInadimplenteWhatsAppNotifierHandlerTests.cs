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

public class AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IWhatsAppNotifier> _notifier = new();
    private readonly Mock<ILogger<AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandler>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandler _handler;

    private static readonly Guid AssinaturaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static readonly AssinaturaAlunoMarcadaInadimplenteEvent Evento =
        new(AssinaturaId, AlunoId, TreinadorId, 3, TestData.Agora);

    public AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandlerTests()
    {
        _notifier.Setup(n => n.Habilitado).Returns(true);
        _notifier.Setup(n => n.SendTemplateAsync(It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _handler = new AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandler(
            _alunoRepo.Object, _notifier.Object, _appSettings, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_HabilitadoFalso_NaoEnvia()
    {
        _notifier.Setup(n => n.Habilitado).Returns(false);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_NaoEnvia()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoSemTelefone_NaoEnvia()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora).Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoComTelefone_EnviaTemplateInadimplente()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, telefone: "11999998888").Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            "11999998888",
            It.Is<WhatsAppTemplateMessage>(m =>
                m.Name == "assinatura_inadimplente" &&
                m.BodyParameters.Contains("Maria") &&
                m.BodyParameters.Any(p => p.Contains("https://app.forzion.tech/aluno/pagamentos"))),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
