using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;

namespace forzion.tech.Tests.Infrastructure.Services;

public class StripeServiceCancelamentoTests
{
    private static StripeService CriarServico(IStripeClient client) =>
        new(Options.Create(new StripeSettings { SecretKey = "sk_test_x" }), TimeProvider.System, NullLogger<StripeService>.Instance, client);

    [Fact]
    public async Task CancelarPaymentIntentAsync_StatusSucceeded_RetornaJaCapturado()
    {
        var client = new FakeStripeClient()
            .Returns(new PaymentIntent { Id = "pi_x", Status = "succeeded" }, HttpMethod.Get);

        var resultado = await CriarServico(client.Object).CancelarPaymentIntentAsync("pi_x");

        resultado.Should().Be(CancelarPaymentIntentResultado.JaCapturado);
    }

    [Fact]
    public async Task CancelarPaymentIntentAsync_StatusCanceled_RetornaJaCancelado()
    {
        var client = new FakeStripeClient()
            .Returns(new PaymentIntent { Id = "pi_x", Status = "canceled" }, HttpMethod.Get);

        var resultado = await CriarServico(client.Object).CancelarPaymentIntentAsync("pi_x");

        resultado.Should().Be(CancelarPaymentIntentResultado.JaCancelado);
    }

    [Fact]
    public async Task CancelarPaymentIntentAsync_StatusCancelavel_CancelaERetornaCancelado()
    {
        var client = new FakeStripeClient()
            .Returns(new PaymentIntent { Id = "pi_x", Status = "requires_payment_method" }, HttpMethod.Get)
            .Returns(new PaymentIntent { Id = "pi_x", Status = "canceled" }, HttpMethod.Post, "/cancel");

        var resultado = await CriarServico(client.Object).CancelarPaymentIntentAsync("pi_x");

        resultado.Should().Be(CancelarPaymentIntentResultado.Cancelado);
    }

    [Fact]
    public async Task CancelarPaymentIntentAsync_CancelLancaUnexpectedStateSucceeded_RetornaJaCapturado()
    {
        var excecao = new StripeException("payment intent unexpected state")
        {
            StripeError = new StripeError
            {
                Code = "payment_intent_unexpected_state",
                PaymentIntent = new PaymentIntent { Status = "succeeded" },
            },
        };
        var client = new FakeStripeClient()
            .Returns(new PaymentIntent { Id = "pi_x", Status = "requires_payment_method" }, HttpMethod.Get)
            .Throws<PaymentIntent>(excecao, HttpMethod.Post, "/cancel");

        var resultado = await CriarServico(client.Object).CancelarPaymentIntentAsync("pi_x");

        resultado.Should().Be(CancelarPaymentIntentResultado.JaCapturado);
    }

    [Fact]
    public async Task CancelarPaymentIntentAsync_CancelLancaUnexpectedStateTerminal_RetornaJaCancelado()
    {
        var excecao = new StripeException("payment intent unexpected state")
        {
            StripeError = new StripeError
            {
                Code = "payment_intent_unexpected_state",
                PaymentIntent = new PaymentIntent { Status = "canceled" },
            },
        };
        var client = new FakeStripeClient()
            .Returns(new PaymentIntent { Id = "pi_x", Status = "requires_payment_method" }, HttpMethod.Get)
            .Throws<PaymentIntent>(excecao, HttpMethod.Post, "/cancel");

        var resultado = await CriarServico(client.Object).CancelarPaymentIntentAsync("pi_x");

        resultado.Should().Be(CancelarPaymentIntentResultado.JaCancelado);
    }
}
