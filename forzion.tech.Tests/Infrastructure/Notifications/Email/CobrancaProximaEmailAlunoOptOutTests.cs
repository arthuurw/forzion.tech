using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class CobrancaProximaEmailAlunoOptOutTests
{
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static readonly CobrancaProximaAlunoEvent Evento =
        new(Guid.NewGuid(), AlunoId, TreinadorId, 149.90m, new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc), TestData.Agora);

    [Fact]
    public async Task HandleAsync_ContaComOptOutEngajamento_BillingEnviaMesmoAssim()
    {
        var emailService = new Mock<IEmailService>();
        emailService.SetupGet(e => e.Habilitado).Returns(true);
        emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var planoPolicy = new Mock<IPlanoNotificationPolicy>();
        planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, true));

        var alunoRepo = new Mock<IAlunoRepository>();
        alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlunoBuilder().ComContaId(ContaId).ComNome("João Aluno").ComEmail(null).Build());

        var contaRepo = new Mock<IContaRepository>();
        contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContaBuilder().ComEmail("joao@conta.com").ComEngajamentoEmailOptOut().Build());

        var handler = new CobrancaProximaEmailAlunoHandler(
            alunoRepo.Object, contaRepo.Object, emailService.Object, planoPolicy.Object,
            Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" }),
            Mock.Of<ILogger<CobrancaProximaEmailAlunoHandler>>());

        await handler.HandleAsync(Evento);

        emailService.Verify(e => e.EnviarAsync(
            "joao@conta.com", It.Is<string>(s => s.Contains("3 dias")), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
