using FluentAssertions;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Alerts;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Alerts;

public class PagamentoEmDisputaAlertHandlerTests
{
    private readonly Mock<ILogger<PagamentoEmDisputaAlertHandler>> _logger = new();
    private readonly PagamentoEmDisputaAlertHandler _handler;

    public PagamentoEmDisputaAlertHandlerTests()
    {
        _handler = new PagamentoEmDisputaAlertHandler(_logger.Object);
    }

    [Fact]
    public async Task HandleAsync_LogaCriticalComCamposEstruturados()
    {
        var evento = new PagamentoEmDisputaEvent(
            PagamentoId: Guid.NewGuid(),
            AssinaturaAlunoId: Guid.NewGuid(),
            Valor: 149.90m,
            MotivoDisputa: "fraudulent",
            OcorridoEm: TestData.Agora);

        await _handler.HandleAsync(evento);

        // Verifica que pelo menos um log Critical foi emitido contendo a palavra CHARGEBACK
        // pra Arthur conseguir filtrar no agregador de logs.
        _logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("CHARGEBACK", StringComparison.Ordinal)),
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
