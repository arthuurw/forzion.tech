using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Moq;

namespace forzion.tech.Tests.Application.Admin.HealthReport;

public class ListarHealthSnapshotsHandlerTests
{
    private readonly Mock<IHealthSnapshotRepository> _repo = new();
    private readonly ListarHealthSnapshotsHandler _handler;

    public ListarHealthSnapshotsHandlerTests() => _handler = new ListarHealthSnapshotsHandler(_repo.Object);

    [Fact]
    public async Task HandleAsync_RetornaSnapshotsMapeados()
    {
        var snapshots = new[]
        {
            HealthSnapshot.Criar("homolog", StatusSaude.Ok, "{}", DateTime.UtcNow),
            HealthSnapshot.Criar("homolog", StatusSaude.Degradado, "{}", DateTime.UtcNow.AddMinutes(-1))
        };
        _repo.Setup(r => r.ListarRecentesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(snapshots);

        var result = await _handler.HandleAsync();

        result.Should().HaveCount(2);
        result[0].StatusGeral.Should().Be(StatusSaude.Ok);
    }

    [Fact]
    public async Task HandleAsync_SemLimite_UsaPadrao30()
    {
        _repo.Setup(r => r.ListarRecentesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HealthSnapshot>());

        await _handler.HandleAsync();

        _repo.Verify(r => r.ListarRecentesAsync(30, It.IsAny<CancellationToken>()), Times.Once);
    }
}
