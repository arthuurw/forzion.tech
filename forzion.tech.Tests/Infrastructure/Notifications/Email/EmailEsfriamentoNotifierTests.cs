using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class EmailEsfriamentoNotifierTests
{
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid ContaId = Guid.NewGuid();

    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly EmailEsfriamentoNotifier _notifier;

    public EmailEsfriamentoNotifierTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _planoPolicy.Setup(p => p.ResolverPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, false));
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlunoBuilder().ComContaId(ContaId).ComNome("Ana").ComEmail("ana@aluno.com").Build());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContaBuilder().Build());

        _notifier = new EmailEsfriamentoNotifier(
            _alunoRepo.Object, _contaRepo.Object, _emailService.Object, _planoPolicy.Object,
            Mock.Of<ILogger<EmailEsfriamentoNotifier>>());
    }

    [Fact]
    public async Task NotificarAsync_TierPro_EnviaLembreteLeveComTemplateCorreto()
    {
        await _notifier.NotificarAsync(AlunoId, TipoNotificacao.LembreteLeve);

        _emailService.Verify(e => e.EnviarAsync(
            "ana@aluno.com",
            It.Is<string>(s => s.Contains("Bora treinar")),
            It.Is<string>(html => html.Contains("Ana") && html.Contains("Registrar treino")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotificarAsync_TierPro_EnviaRecuperacaoComTemplateCorreto()
    {
        await _notifier.NotificarAsync(AlunoId, TipoNotificacao.Recuperacao);

        _emailService.Verify(e => e.EnviarAsync(
            "ana@aluno.com",
            It.Is<string>(s => s.Contains("retomar")),
            It.Is<string>(html => html.Contains("Voltar a treinar")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotificarAsync_TierAbaixoDePro_NaoEnvia()
    {
        _planoPolicy.Setup(p => p.ResolverPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(false, false));

        await _notifier.NotificarAsync(AlunoId, TipoNotificacao.LembreteLeve);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotificarAsync_ContaComOptOut_NaoEnvia()
    {
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContaBuilder().ComEngajamentoEmailOptOut().Build());

        await _notifier.NotificarAsync(AlunoId, TipoNotificacao.LembreteLeve);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotificarAsync_TipoNaoEsfriamento_NaoEnvia()
    {
        await _notifier.NotificarAsync(AlunoId, TipoNotificacao.Reforco);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
