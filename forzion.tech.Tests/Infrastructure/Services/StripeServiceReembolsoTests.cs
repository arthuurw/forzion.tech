using FluentAssertions;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;

namespace forzion.tech.Tests.Infrastructure.Services;

public class StripeServiceReembolsoTests
{
    // Expõe o RefundRequestOptions (protected) sem disparar a chamada HTTP real do Stripe SDK.
    private sealed class StripeServiceTestDouble(IOptions<StripeSettings> settings)
        : StripeService(settings, TimeProvider.System, NullLogger<StripeService>.Instance)
    {
        public RequestOptions ExporRefundRequestOptions(Guid pagamentoId) => RefundRequestOptions(pagamentoId);
    }

    private static StripeServiceTestDouble Criar() =>
        new(Options.Create(new StripeSettings { SecretKey = "sk_test_x" }));

    [Fact]
    public void RefundRequestOptions_IdempotencyKeyDerivadaDoPagamento()
    {
        var pagamentoId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var opts = Criar().ExporRefundRequestOptions(pagamentoId);

        opts.IdempotencyKey.Should().Be($"refund-{pagamentoId:N}");
    }

    [Fact]
    public void RefundRequestOptions_MesmoPagamento_MesmaKey()
    {
        var pagamentoId = Guid.NewGuid();
        var svc = Criar();

        svc.ExporRefundRequestOptions(pagamentoId).IdempotencyKey
            .Should().Be(svc.ExporRefundRequestOptions(pagamentoId).IdempotencyKey);
    }

    [Fact]
    public void RefundRequestOptions_PagamentosDistintos_KeysDiferentes()
    {
        var svc = Criar();

        svc.ExporRefundRequestOptions(Guid.NewGuid()).IdempotencyKey
            .Should().NotBe(svc.ExporRefundRequestOptions(Guid.NewGuid()).IdempotencyKey);
    }
}
