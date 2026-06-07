using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Lgpd;
using forzion.tech.Tests.Builders;
using Moq;

namespace forzion.tech.Tests.Application.Lgpd;

public class ListarContasElegivelPurgaLgpdHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 6, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly ListarContasElegivelPurgaLgpdHandler _handler;

    public ListarContasElegivelPurgaLgpdHandlerTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(Agora));
        _handler = new ListarContasElegivelPurgaLgpdHandler(_contaRepo.Object, _timeProvider.Object);
    }

    [Fact]
    public async Task HandleAsync_UsaThresholdDeCincoAnos()
    {
        var threshold = Agora.AddYears(-5);
        _contaRepo.Setup(r => r.ListarElegivelPurgaLgpdAsync(threshold, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await _handler.HandleAsync();

        _contaRepo.Verify(r => r.ListarElegivelPurgaLgpdAsync(threshold, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RetornaIdsDoRepositorio()
    {
        var ids = new[] { TestData.NextGuid(), TestData.NextGuid() };
        _contaRepo.Setup(r => r.ListarElegivelPurgaLgpdAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

        var resultado = await _handler.HandleAsync();

        resultado.Should().BeEquivalentTo(ids);
    }
}
