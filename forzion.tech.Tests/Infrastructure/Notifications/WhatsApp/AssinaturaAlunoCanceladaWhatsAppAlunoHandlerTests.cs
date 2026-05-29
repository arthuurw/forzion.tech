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

public class AssinaturaAlunoCanceladaWhatsAppAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IWhatsAppNotifier> _notifier = new();
    private readonly Mock<ILogger<AssinaturaAlunoCanceladaWhatsAppAlunoHandler>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly AssinaturaAlunoCanceladaWhatsAppAlunoHandler _handler;

    private static readonly Guid AssinaturaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static readonly AssinaturaAlunoCanceladaEvent Evento =
        new(AssinaturaId, AlunoId, TreinadorId, 150m, TestData.Agora);

    public AssinaturaAlunoCanceladaWhatsAppAlunoHandlerTests()
    {
        _notifier.Setup(n => n.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _handler = new AssinaturaAlunoCanceladaWhatsAppAlunoHandler(
            _alunoRepo.Object, _notifier.Object, _appSettings, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_NaoEnvia()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoSemTelefone_NaoEnvia()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora).Value; // sem telefone
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoComTelefone_EnviaConfirmacaoComLinkPortal()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, telefone: "11999998888").Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendAsync(
            "11999998888",
            It.Is<string>(msg =>
                msg.Contains("Maria") &&
                msg.Contains("cancelada", StringComparison.OrdinalIgnoreCase) &&
                msg.Contains("https://app.forzion.tech/aluno/pagamentos") &&
                !msg.Contains("stripe.com")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
