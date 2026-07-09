using FluentAssertions;
using forzion.tech.Api.Services;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Api.Services;

public class RelatorioSaudeDiarioServiceTests
{
    private static readonly TimeOnly Hora = new(7, 0);

    private static HealthReportConfig Config(bool ativo) =>
        HealthReportConfig.Criar(
            ativo,
            Hora,
            ativo ? new[] { "admin@forzion.tech" } : Array.Empty<string>(),
            true, true, true, true,
            DateTime.UtcNow).Value;

    [Fact]
    public void DeveEnviar_Inativo_RetornaFalse()
    {
        var config = Config(ativo: false);
        var agora = new DateTime(2026, 5, 26, 8, 0, 0, DateTimeKind.Utc);

        RelatorioSaudeDiarioService.DeveEnviar(config, agora).Should().BeFalse();
    }

    [Fact]
    public void DeveEnviar_AntesDaHora_RetornaFalse()
    {
        var config = Config(ativo: true);
        var agora = new DateTime(2026, 5, 26, 6, 59, 0, DateTimeKind.Utc);

        RelatorioSaudeDiarioService.DeveEnviar(config, agora).Should().BeFalse();
    }

    [Fact]
    public void DeveEnviar_DevidoENaoEnviado_RetornaTrue()
    {
        var config = Config(ativo: true);
        var agora = new DateTime(2026, 5, 26, 7, 30, 0, DateTimeKind.Utc);

        RelatorioSaudeDiarioService.DeveEnviar(config, agora).Should().BeTrue();
    }

    [Fact]
    public void DeveEnviar_JaEnviadoHoje_RetornaFalse()
    {
        var config = Config(ativo: true);
        config.MarcarEnviado(new DateTime(2026, 5, 26, 7, 0, 0, DateTimeKind.Utc));
        var agora = new DateTime(2026, 5, 26, 7, 30, 0, DateTimeKind.Utc);

        RelatorioSaudeDiarioService.DeveEnviar(config, agora).Should().BeFalse();
    }

    [Fact]
    public void DeveEnviar_EnviadoOntem_RetornaTrue()
    {
        var config = Config(ativo: true);
        config.MarcarEnviado(new DateTime(2026, 5, 25, 7, 0, 0, DateTimeKind.Utc));
        var agora = new DateTime(2026, 5, 26, 7, 30, 0, DateTimeKind.Utc);

        RelatorioSaudeDiarioService.DeveEnviar(config, agora).Should().BeTrue();
    }

    private static HealthReport Report() => new()
    {
        Ambiente = "homolog",
        CapturadoEm = DateTime.UtcNow,
        StatusGeral = StatusSaude.Ok,
    };

    private static RelatorioSaudeDiarioService BuildService(
        Mock<IHealthReportSender> sender,
        Mock<IUnitOfWork> unitOfWork,
        DateTime agora)
    {
        var configRepo = new Mock<IHealthReportConfigRepository>();
        configRepo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Config(ativo: true));

        var collector = new Mock<IHealthReportCollector>();
        collector.Setup(c => c.ColetarAsync(It.IsAny<HealthReportConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Report());

        var snapshotRepo = new Mock<IHealthSnapshotRepository>();
        snapshotRepo.Setup(r => r.AdicionarAsync(It.IsAny<HealthSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(configRepo.Object);
        services.AddSingleton(collector.Object);
        services.AddSingleton(snapshotRepo.Object);
        services.AddSingleton(sender.Object);
        services.AddSingleton(unitOfWork.Object);

        var timeProvider = new FakeTimeProvider(agora);
        return new RelatorioSaudeDiarioService(
            services.BuildServiceProvider(),
            timeProvider,
            NullLogger<RelatorioSaudeDiarioService>.Instance);
    }

    [Fact]
    public async Task ProcessarAsync_EnviaAposCommit()
    {
        var chamadas = new List<string>();
        var agora = new DateTime(2026, 5, 26, 7, 30, 0, DateTimeKind.Utc);

        var sender = new Mock<IHealthReportSender>();
        sender.Setup(s => s.EnviarAsync(It.IsAny<HealthReport>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => chamadas.Add("enviar"))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => chamadas.Add("commit"))
            .Returns(Task.CompletedTask);

        var service = BuildService(sender, unitOfWork, agora);

        await service.ProcessarAsync(CancellationToken.None);

        chamadas.Should().Equal("commit", "enviar");
    }

    [Fact]
    public async Task ProcessarAsync_CommitLanca_NaoEnvia()
    {
        var chamadas = new List<string>();
        var agora = new DateTime(2026, 5, 26, 7, 30, 0, DateTimeKind.Utc);

        var sender = new Mock<IHealthReportSender>();
        sender.Setup(s => s.EnviarAsync(It.IsAny<HealthReport>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => chamadas.Add("enviar"))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("commit falhou"));

        var service = BuildService(sender, unitOfWork, agora);

        await FluentActions.Awaiting(() => service.ProcessarAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();

        sender.Verify(
            s => s.EnviarAsync(It.IsAny<HealthReport>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessarAsync_EnvioFalhaPosCommit_NaoPropagaEJaEstaPersistido()
    {
        var agora = new DateTime(2026, 5, 26, 7, 30, 0, DateTimeKind.Utc);

        var sender = new Mock<IHealthReportSender>();
        sender.Setup(s => s.EnviarAsync(It.IsAny<HealthReport>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("envio falhou"));

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(sender, unitOfWork, agora);

        await service.ProcessarAsync(CancellationToken.None);

        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
