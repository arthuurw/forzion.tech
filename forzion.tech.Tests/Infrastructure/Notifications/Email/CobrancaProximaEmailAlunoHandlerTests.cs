using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class CobrancaProximaEmailAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly Mock<ILogger<CobrancaProximaEmailAlunoHandler>> _logger = new();
    private readonly CobrancaProximaEmailAlunoHandler _handler;

    private static readonly Guid AssinaturaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly DateTime DataProximaCobranca = new(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc);

    private static readonly CobrancaProximaAlunoEvent Evento =
        new(AssinaturaId, AlunoId, TreinadorId, 149.90m, DataProximaCobranca, TestData.Agora);

    public CobrancaProximaEmailAlunoHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, true));

        _handler = new CobrancaProximaEmailAlunoHandler(
            _alunoRepo.Object, _contaRepo.Object, _emailService.Object,
            _planoPolicy.Object,
            Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" }),
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_NaoEnvia()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CanalEmailBloqueadoPeloPlano_NaoEnvia()
    {
        _planoPolicy.Setup(p => p.ResolverPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(false, false));

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_EnviaComValorEData()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlunoBuilder().ComNome("João Aluno").ComEmail("joao@aluno.com").Build());

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "joao@aluno.com",
            It.Is<string>(s => s.Contains("3 dias")),
            It.Is<string>(html => html.Contains("Aluno")
                && html.Contains("149,90")
                && html.Contains("09/06/2026")
                && html.Contains("https://app.forzion.tech/aluno/assinatura")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
