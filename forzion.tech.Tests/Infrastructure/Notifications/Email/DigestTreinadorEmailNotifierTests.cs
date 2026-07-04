using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class DigestTreinadorEmailNotifierTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid ContaId = Guid.NewGuid();

    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly DigestTreinadorEmailNotifier _notifier;

    public DigestTreinadorEmailNotifierTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, false));
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TreinadorBuilder().ComContaId(ContaId).ComNome("Carlos").Build());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContaBuilder().ComEmail("carlos@treino.com").Build());

        _notifier = new DigestTreinadorEmailNotifier(
            _treinadorRepo.Object, _contaRepo.Object, _emailService.Object, _planoPolicy.Object,
            Mock.Of<ILogger<DigestTreinadorEmailNotifier>>());
    }

    [Fact]
    public async Task NotificarAsync_TierPro_EnviaComContagens()
    {
        await _notifier.NotificarAsync(TreinadorId, treinaram: 3, naoTreinaram: 2);

        _emailService.Verify(e => e.EnviarAsync(
            "carlos@treino.com",
            It.Is<string>(s => s.Contains("Resumo")),
            It.Is<string>(html => html.Contains("Carlos") && html.Contains("3") && html.Contains("2")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotificarAsync_TierAbaixoDePro_NaoEnvia()
    {
        _planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(false, false));

        await _notifier.NotificarAsync(TreinadorId, 3, 2);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotificarAsync_ContaComOptOut_NaoEnvia()
    {
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContaBuilder().ComEmail("carlos@treino.com").ComEngajamentoEmailOptOut().Build());

        await _notifier.NotificarAsync(TreinadorId, 3, 2);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
