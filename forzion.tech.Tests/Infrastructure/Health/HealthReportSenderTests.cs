using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Health;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Health;

public class HealthReportSenderTests
{
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ILogger<HealthReportSender>> _logger = new();
    private readonly HealthReportSender _sender;

    public HealthReportSenderTests()
    {
        _sender = new HealthReportSender(_email.Object, _logger.Object);
    }

    [Fact]
    public async Task EnviarAsync_EmailServiceLanca_LogaErroComEmailMascarado()
    {
        const string emailRaw = "admin@forzion.tech";
        _email
            .Setup(e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var report = new HealthReport
        {
            Ambiente = "Test",
            StatusGeral = StatusSaude.Ok,
            CapturadoEm = DateTime.UtcNow
        };

        await _sender.EnviarAsync(report, [emailRaw]);

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => !v.ToString()!.Contains(emailRaw)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
