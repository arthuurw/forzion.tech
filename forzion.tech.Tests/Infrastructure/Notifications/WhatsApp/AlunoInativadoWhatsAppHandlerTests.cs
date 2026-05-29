using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.WhatsApp;

public class AlunoInativadoWhatsAppHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IWhatsAppNotifier> _notifier = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly Mock<ILogger<AlunoInativadoWhatsAppHandler>> _logger = new();
    private readonly AlunoInativadoWhatsAppHandler _handler;

    private static readonly Guid AlunoId = Guid.NewGuid();

    private static readonly AlunoInativadoEvent Evento = new(AlunoId, TestData.Agora);

    public AlunoInativadoWhatsAppHandlerTests()
    {
        _notifier.Setup(n => n.Habilitado).Returns(true);
        _notifier.Setup(n => n.SendTemplateAsync(It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _planoPolicy.Setup(p => p.ResolverPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, true));

        _handler = new AlunoInativadoWhatsAppHandler(
            _alunoRepo.Object, _notifier.Object, _planoPolicy.Object, _logger.Object);
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
    public async Task HandleAsync_AlunoComTelefone_EnviaTemplateAlunoInativado()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, telefone: "11999998888").Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            "11999998888",
            It.Is<WhatsAppTemplateMessage>(m =>
                m.Name == "aluno_inativado" &&
                m.BodyParameters.Contains("Maria")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PlanoSemWhatsApp_NaoEnvia()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, telefone: "11999998888").Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _planoPolicy.Setup(p => p.ResolverPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, false));

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
