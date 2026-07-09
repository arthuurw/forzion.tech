using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Admin.HealthReport;

public class ExecutarRelatorioSaudeHandlerTests
{
    private readonly Mock<IHealthReportConfigRepository> _configRepo = new();
    private readonly Mock<IHealthReportCollector> _collector = new();
    private readonly Mock<IHealthSnapshotRepository> _snapshotRepo = new();
    private readonly Mock<IHealthReportSender> _sender = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<ExecutarRelatorioSaudeHandler>> _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero));
    private readonly ExecutarRelatorioSaudeHandler _handler;

    public ExecutarRelatorioSaudeHandlerTests() =>
        _handler = new ExecutarRelatorioSaudeHandler(
            _configRepo.Object, _collector.Object, _snapshotRepo.Object, _sender.Object, _unitOfWork.Object, _time,
            _logger.Object);

    private static HealthReportConfig Config() =>
        HealthReportConfig.Criar(true, new TimeOnly(7, 0), new[] { "ops@forzion.tech" },
            true, true, true, true, DateTime.UtcNow).Value;

    private static forzion.tech.Application.UseCases.Admin.HealthReport.HealthReport Report() => new()
    {
        Ambiente = "Homolog",
        CapturadoEm = DateTime.UtcNow,
        StatusGeral = StatusSaude.Degradado,
    };

    [Fact]
    public async Task HandleAsync_SemConfig_LancaEstadoInconsistente()
    {
        _configRepo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync((HealthReportConfig?)null);

        var act = async () => await _handler.HandleAsync();

        await act.Should().ThrowAsync<EstadoInconsistenteException>();
    }

    [Fact]
    public async Task HandleAsync_ComConfig_ColetaPersisteEnviaECommita()
    {
        var config = Config();
        _configRepo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var report = Report();
        _collector.Setup(c => c.ColetarAsync(config, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Ambiente.Should().Be("Homolog");
        result.Value.StatusGeral.Should().Be(StatusSaude.Degradado);
        result.Value.EmailEnviado.Should().BeTrue();
        _snapshotRepo.Verify(r => r.AdicionarAsync(It.IsAny<HealthSnapshot>(), It.IsAny<CancellationToken>()), Times.Once);
        _sender.Verify(s => s.EnviarAsync(report, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_EnviaAposCommit()
    {
        var config = Config();
        _configRepo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);
        _collector.Setup(c => c.ColetarAsync(config, It.IsAny<CancellationToken>())).ReturnsAsync(Report());

        var chamadas = new List<string>();
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => chamadas.Add("commit"))
            .Returns(Task.CompletedTask);
        _sender.Setup(s => s.EnviarAsync(It.IsAny<forzion.tech.Application.UseCases.Admin.HealthReport.HealthReport>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => chamadas.Add("enviar"))
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync();

        chamadas.Should().Equal("commit", "enviar", "commit");
    }

    [Fact]
    public async Task HandleAsync_CommitLanca_NaoEnvia()
    {
        var config = Config();
        _configRepo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);
        _collector.Setup(c => c.ColetarAsync(config, It.IsAny<CancellationToken>())).ReturnsAsync(Report());
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("commit falhou"));

        var act = async () => await _handler.HandleAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        _sender.Verify(
            s => s.EnviarAsync(It.IsAny<forzion.tech.Application.UseCases.Admin.HealthReport.HealthReport>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EnvioFalhaPosCommit_NaoPropagaERetornaSucessoELogaCritical()
    {
        var config = Config();
        _configRepo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);
        _collector.Setup(c => c.ColetarAsync(config, It.IsAny<CancellationToken>())).ReturnsAsync(Report());
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _sender.Setup(s => s.EnviarAsync(It.IsAny<forzion.tech.Application.UseCases.Admin.HealthReport.HealthReport>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("envio falhou"));

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.EmailEnviado.Should().BeFalse();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EnvioCancelaPosCommit_Propaga()
    {
        var config = Config();
        _configRepo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);
        _collector.Setup(c => c.ColetarAsync(config, It.IsAny<CancellationToken>())).ReturnsAsync(Report());
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _sender.Setup(s => s.EnviarAsync(It.IsAny<forzion.tech.Application.UseCases.Admin.HealthReport.HealthReport>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await _handler.HandleAsync();

        await act.Should().ThrowAsync<OperationCanceledException>();
        _logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
