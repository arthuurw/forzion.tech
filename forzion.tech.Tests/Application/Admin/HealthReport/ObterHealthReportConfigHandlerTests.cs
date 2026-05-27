using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Entities;
using Moq;

namespace forzion.tech.Tests.Application.Admin.HealthReport;

public class ObterHealthReportConfigHandlerTests
{
    private readonly Mock<IHealthReportConfigRepository> _repo = new();
    private readonly ObterHealthReportConfigHandler _handler;

    public ObterHealthReportConfigHandlerTests() => _handler = new ObterHealthReportConfigHandler(_repo.Object);

    [Fact]
    public async Task HandleAsync_ComConfig_RetornaResponse()
    {
        var config = HealthReportConfig.Criar(true, new TimeOnly(7, 0), new[] { "admin@forzion.tech" },
            true, true, true, true, DateTime.UtcNow);
        _repo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var result = await _handler.HandleAsync();

        result.Should().NotBeNull();
        result!.Id.Should().Be(config.Id);
        result.Ativo.Should().BeTrue();
        result.Destinatarios.Should().ContainSingle().Which.Should().Be("admin@forzion.tech");
    }

    [Fact]
    public async Task HandleAsync_SemConfig_RetornaNull()
    {
        _repo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync((HealthReportConfig?)null);

        var result = await _handler.HandleAsync();

        result.Should().BeNull();
    }
}
