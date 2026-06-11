using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Outbox;
using forzion.tech.Infrastructure.Outbox.Handlers;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Outbox;

public class EvidenciaDisputaEfeitoHandlerTests
{
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly EvidenciaDisputaEfeitoHandler _handler;

    public EvidenciaDisputaEfeitoHandlerTests()
    {
        _handler = new EvidenciaDisputaEfeitoHandler(_stripeService.Object);
    }

    [Fact]
    public void Tipo_RetornaFxEvidenciaDisputa()
    {
        _handler.Tipo.Should().Be("fx:evidencia_disputa");
    }

    [Fact]
    public async Task ExecutarAsync_PayloadValido_ChamaStripeComCamposCorretos()
    {
        var dataAtivacao = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var dataPagamento = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var pagamentoId = Guid.NewGuid();
        var payload = new EvidenciaDisputaPayload("dp_test123", "aluno@x.com", dataAtivacao, dataPagamento, pagamentoId);
        var json = JsonSerializer.Serialize(payload);

        await _handler.ExecutarAsync(json);

        _stripeService.Verify(s => s.EnviarEvidenciaDisputaAsync(
            "dp_test123",
            It.Is<DisputaEvidencia>(e =>
                e.EmailCliente == "aluno@x.com" &&
                e.DataAtivacao == dataAtivacao &&
                e.DataUltimaAtividade == null &&
                e.DataUltimoPagamento == dataPagamento),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecutarAsync_PayloadSemEmail_ChamaStripeComEmailNull()
    {
        var pagamentoId = Guid.NewGuid();
        var payload = new EvidenciaDisputaPayload("dp_sem_email", null, null, null, pagamentoId);
        var json = JsonSerializer.Serialize(payload);

        await _handler.ExecutarAsync(json);

        _stripeService.Verify(s => s.EnviarEvidenciaDisputaAsync(
            "dp_sem_email",
            It.Is<DisputaEvidencia>(e => e.EmailCliente == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecutarAsync_StripeServiceLancaExcecao_Propaga()
    {
        var payload = new EvidenciaDisputaPayload("dp_fail", "x@x.com", null, null, Guid.NewGuid());
        var json = JsonSerializer.Serialize(payload);

        _stripeService
            .Setup(s => s.EnviarEvidenciaDisputaAsync(It.IsAny<string>(), It.IsAny<DisputaEvidencia>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("stripe down"));

        // Exceção propaga para que o worker faça retry.
        var act = () => _handler.ExecutarAsync(json);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("stripe down");
    }

    [Fact]
    public async Task ExecutarAsync_PayloadInvalido_LancaInvalidOperationException()
    {
        var act = () => _handler.ExecutarAsync("null");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
