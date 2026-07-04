using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.WhatsApp;

public class TreinoDisponibilizadoWhatsAppHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IWhatsAppNotifier> _notifier = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly Mock<ILogger<TreinoDisponibilizadoWhatsAppHandler>> _logger = new();
    private readonly TreinoDisponibilizadoWhatsAppHandler _handler;

    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly TreinoDisponibilizadoEvent Evento =
        new(AlunoId, Guid.NewGuid(), Guid.NewGuid(), TestData.Agora);

    public TreinoDisponibilizadoWhatsAppHandlerTests()
    {
        _notifier.SetupGet(n => n.Habilitado).Returns(true);
        _planoPolicy.Setup(p => p.ResolverPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, true));
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContaBuilder().Build());

        _handler = new TreinoDisponibilizadoWhatsAppHandler(
            _alunoRepo.Object, _contaRepo.Object, _notifier.Object, _planoPolicy.Object, _logger.Object);
    }

    private void ComAluno(Aluno aluno) =>
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

    [Fact]
    public async Task HandleAsync_NotifierDesabilitado_NaoEnvia()
    {
        _notifier.SetupGet(n => n.Habilitado).Returns(false);
        ComAluno(Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, telefone: "11999998888").Value);

        await _handler.HandleAsync(Evento);

        VerificaNaoEnviou();
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_NaoEnvia()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        VerificaNaoEnviou();
    }

    [Fact]
    public async Task HandleAsync_AlunoSemTelefone_NaoEnvia()
    {
        ComAluno(Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora).Value);

        await _handler.HandleAsync(Evento);

        VerificaNaoEnviou();
    }

    [Fact]
    public async Task HandleAsync_TierAbaixoDeProPlus_NaoEnvia()
    {
        ComAluno(Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, telefone: "11999998888").Value);
        _planoPolicy.Setup(p => p.ResolverPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, false));

        await _handler.HandleAsync(Evento);

        VerificaNaoEnviou();
    }

    [Fact]
    public async Task HandleAsync_OptOutAtivo_NaoEnvia()
    {
        var contaId = Guid.NewGuid();
        ComAluno(Aluno.Criar(contaId, "Maria", TestData.Agora, telefone: "11999998888").Value);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContaBuilder().ComEngajamentoEmailOptOut().Build());

        await _handler.HandleAsync(Evento);

        VerificaNaoEnviou();
    }

    [Fact]
    public async Task HandleAsync_ProPlusComTelefoneSemOptOut_EnviaTemplateNovoTreino()
    {
        ComAluno(Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, telefone: "11999998888").Value);

        await _handler.HandleAsync(Evento);

        _notifier.Verify(n => n.SendTemplateAsync(
            "11999998888",
            It.Is<WhatsAppTemplateMessage>(m =>
                m.Name == "novo_treino_disponivel" &&
                m.BodyParameters.Contains("Maria")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void VerificaNaoEnviou() =>
        _notifier.Verify(n => n.SendTemplateAsync(
            It.IsAny<string>(), It.IsAny<WhatsAppTemplateMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
}
