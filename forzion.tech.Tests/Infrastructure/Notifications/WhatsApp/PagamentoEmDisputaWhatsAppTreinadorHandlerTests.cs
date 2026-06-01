using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.WhatsApp;

public class PagamentoEmDisputaWhatsAppTreinadorHandlerTests
{
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IWhatsAppNotifier> _notifier = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly Mock<ILogger<PagamentoEmDisputaWhatsAppTreinadorHandler>> _logger = new();
    private readonly PagamentoEmDisputaWhatsAppTreinadorHandler _handler;

    private static readonly Guid AssinaturaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();

    private static readonly PagamentoEmDisputaEvent Evento =
        new(Guid.NewGuid(), AssinaturaId, 200m, "fraud", TestData.Agora);

    public PagamentoEmDisputaWhatsAppTreinadorHandlerTests()
    {
        _notifier.Setup(n => n.Habilitado).Returns(true);
        _notifier.Setup(n => n.SendTemplateAsync(It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, true));

        _handler = new PagamentoEmDisputaWhatsAppTreinadorHandler(
            _assinaturaRepo.Object, _treinadorRepo.Object, _alunoRepo.Object,
            _notifier.Object, _planoPolicy.Object, _logger.Object);
    }

    private AssinaturaAluno AssinaturaValida() =>
        AssinaturaAluno.Criar(Guid.NewGuid(), PacoteId, TreinadorId, AlunoId, 200m, TestData.Agora).Value;

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
    public async Task HandleAsync_AssinaturaNaoEncontrada_NaoEnvia()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_NaoEnvia()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
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
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", TestData.Agora).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorComTelefone_EnviaTemplatePagamentoEmDisputa()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", TestData.Agora, telefone: "11999998888").Value;
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora).Value;
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            "11999998888",
            It.Is<WhatsAppTemplateMessage>(m =>
                m.Name == "pagamento_em_disputa" &&
                m.BodyParameters.Contains("Carlos") &&
                m.BodyParameters.Contains("Maria") &&
                m.BodyParameters.Contains("200,00")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_UsaFallbackNome()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", TestData.Agora, telefone: "11999998888").Value;
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            "11999998888",
            It.Is<WhatsAppTemplateMessage>(m =>
                m.Name == "pagamento_em_disputa" &&
                m.BodyParameters.Contains("Um aluno")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PlanoSemWhatsApp_NaoEnvia()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", TestData.Agora, telefone: "11999998888").Value;
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(AssinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssinaturaValida());
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, false));

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
