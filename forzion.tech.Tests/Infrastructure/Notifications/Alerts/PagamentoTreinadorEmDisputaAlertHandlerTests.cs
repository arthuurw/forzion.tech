using FluentAssertions;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Alerts;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Alerts;

public class PagamentoTreinadorEmDisputaAlertHandlerTests
{
    private readonly Mock<ILogger<PagamentoTreinadorEmDisputaAlertHandler>> _logger = new();
    private readonly PagamentoTreinadorEmDisputaAlertHandler _handler;

    public PagamentoTreinadorEmDisputaAlertHandlerTests()
    {
        _handler = new PagamentoTreinadorEmDisputaAlertHandler(_logger.Object);
    }

    [Fact]
    public async Task HandleAsync_LogaCriticalComPagamentoTreinadorIdETreinadorIdEValor()
    {
        var pagamentoTreinadorId = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();
        var evento = new PagamentoTreinadorEmDisputaEvent(
            PagamentoTreinadorId: pagamentoTreinadorId,
            TreinadorId: treinadorId,
            Valor: 299.90m,
            OcorridoEm: TestData.Agora);

        await _handler.HandleAsync(evento);

        _logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("CHARGEBACK", StringComparison.Ordinal) &&
                    v.ToString()!.Contains(pagamentoTreinadorId.ToString(), StringComparison.Ordinal) &&
                    v.ToString()!.Contains(treinadorId.ToString(), StringComparison.Ordinal) &&
                    v.ToString()!.Contains("299.90", StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EventoNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
