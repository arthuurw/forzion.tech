using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.WhatsApp;

public class TreinadorAprovadoWhatsAppHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IWhatsAppNotifier> _notifier = new();
    private readonly Mock<ILogger<TreinadorAprovadoWhatsAppHandler>> _logger = new();
    private readonly TreinadorAprovadoWhatsAppHandler _handler;

    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static readonly TreinadorAprovadoEvent Evento =
        new(TreinadorId, Guid.NewGuid(), TestData.Agora);

    public TreinadorAprovadoWhatsAppHandlerTests()
    {
        _notifier.Setup(n => n.Habilitado).Returns(true);
        _notifier.Setup(n => n.SendTemplateAsync(It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new TreinadorAprovadoWhatsAppHandler(
            _treinadorRepo.Object, _notifier.Object, _logger.Object);
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
    public async Task HandleAsync_TreinadorNaoEncontrado_NaoEnvia()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemTelefone_NaoEnvia()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", TestData.Agora).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorComTelefone_EnviaTemplateTreinadorAprovado()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", TestData.Agora, telefone: "11999998888").Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            "11999998888",
            It.Is<WhatsAppTemplateMessage>(m =>
                m.Name == "treinador_aprovado" &&
                m.BodyParameters.Contains("Carlos")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
