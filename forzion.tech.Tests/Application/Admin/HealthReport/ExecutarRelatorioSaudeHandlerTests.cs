using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
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
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero));
    private readonly ExecutarRelatorioSaudeHandler _handler;

    public ExecutarRelatorioSaudeHandlerTests() =>
        _handler = new ExecutarRelatorioSaudeHandler(
            _configRepo.Object, _collector.Object, _snapshotRepo.Object, _sender.Object, _unitOfWork.Object, _time);

    [Fact]
    public async Task HandleAsync_SemConfig_LancaDomainException()
    {
        _configRepo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync((HealthReportConfig?)null);

        var act = async () => await _handler.HandleAsync();

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task HandleAsync_ComConfig_ColetaPersisteEnviaECommita()
    {
        var config = HealthReportConfig.Criar(true, new TimeOnly(7, 0), new[] { "ops@forzion.tech" },
            true, true, true, true, DateTime.UtcNow).Value;
        _configRepo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var report = new forzion.tech.Application.UseCases.Admin.HealthReport.HealthReport
        {
            Ambiente = "Homolog",
            CapturadoEm = DateTime.UtcNow,
            StatusGeral = StatusSaude.Degradado
        };
        _collector.Setup(c => c.ColetarAsync(config, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        var result = await _handler.HandleAsync();

        result.Ambiente.Should().Be("Homolog");
        result.StatusGeral.Should().Be(StatusSaude.Degradado);
        _snapshotRepo.Verify(r => r.AdicionarAsync(It.IsAny<HealthSnapshot>(), It.IsAny<CancellationToken>()), Times.Once);
        _sender.Verify(s => s.EnviarAsync(report, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
