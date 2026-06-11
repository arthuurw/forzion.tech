using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

/// <summary>
/// Cenários de lógica temporal com tempo controlado via <see cref="FakeTimeProvider"/>
/// (harness fase 1 — F1.6). Sem tempo real nem Thread.Sleep.
/// </summary>
public class AgendamentoCobrancaTemporalTests
{
    private const string ValidSig = "t=1,v1=abc";
    private static readonly DateTimeOffset Instante = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<IPagamentoTreinadorRepository> _pagamentoTreinadorRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<ProcessarWebhookStripeHandler>> _logger = new();
    private readonly FakeTimeProvider _time = new(Instante);
    private readonly ProcessarWebhookStripeHandler _handler;

    public AgendamentoCobrancaTemporalTests()
    {
        _handler = new ProcessarWebhookStripeHandler(
            _pagamentoRepo.Object, _assinaturaRepo.Object, _contaRecebimentoRepo.Object,
            _pagamentoTreinadorRepo.Object, Mock.Of<IAssinaturaTreinadorRepository>(), Mock.Of<ITreinadorRepository>(),
            Mock.Of<IAlunoRepository>(), Mock.Of<IContaRepository>(),
            _stripeService.Object, _unitOfWork.Object, Mock.Of<IOutboxEnfileirador>(), _time, _logger.Object);

        _stripeService.Setup(s => s.ValidarWebhookAsync(It.IsAny<string>(), ValidSig))
            .ReturnsAsync(true);
    }

    private static string PaymentIntentPayload(string type, string paymentIntentId) =>
        "{\"type\":\"" + type + "\",\"data\":{\"object\":{\"id\":\"" + paymentIntentId + "\"}}}";

    [Fact]
    public async Task PagamentoConfirmado_AgendaProximaCobranca_ExatamenteUmMesAposOInstanteControlado()
    {
        var agora = _time.GetUtcNow().UtcDateTime;
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, agora).Value;
        pagamento.DefinirDadosPix("pi_t1", "qr", "url", agora.AddHours(1), agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, agora).Value;

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_t1"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        // Determinístico: próxima cobrança = instante fixo + 1 mês, independente do relógio real.
        assinatura.DataProximaCobranca.Should().Be(Instante.UtcDateTime.AddMonths(1));
    }

    [Fact]
    public async Task PagamentoConfirmado_AposAvancarOTempo_AgendaComBaseNoNovoInstante()
    {
        _time.Advance(TimeSpan.FromDays(40));
        var agora = _time.GetUtcNow().UtcDateTime;

        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, agora).Value;
        pagamento.DefinirDadosPix("pi_t2", "qr", "url", agora.AddHours(1), agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, agora).Value;

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_t2"), ValidSig));

        assinatura.DataProximaCobranca.Should().Be(Instante.UtcDateTime.AddDays(40).AddMonths(1));
    }
}
