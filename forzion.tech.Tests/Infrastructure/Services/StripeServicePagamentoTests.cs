using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Stripe;

namespace forzion.tech.Tests.Infrastructure.Services;

public class StripeServicePagamentoTests
{
    private static readonly DateTimeOffset Instante = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    private static StripeService CriarServico(IStripeClient client, TimeProvider? time = null) =>
        new(Options.Create(new StripeSettings { SecretKey = "sk_test_x" }), time ?? new FakeTimeProvider(Instante), NullLogger<StripeService>.Instance, client);

    private static PaymentIntent PixIntent(string id) => new()
    {
        Id = id,
        NextAction = new PaymentIntentNextAction
        {
            PixDisplayQrCode = new PaymentIntentNextActionPixDisplayQrCode
            {
                Data = "00020126pix-payload",
                ImageUrlPng = "https://stripe.test/qr.png",
            },
        },
    };

    private static PaymentIntent PixIntentSemDados(string id) => new()
    {
        Id = id,
        NextAction = new PaymentIntentNextAction { PixDisplayQrCode = null },
    };

    [Fact]
    public async Task CriarPixPaymentIntentAsync_SemDadosPix_LancaInvalidOperation()
    {
        var fake = new FakeStripeClient().Returns(PixIntentSemDados("pi_pix"));

        var acao = () => CriarServico(fake.Object).CriarPixPaymentIntentAsync(100m, "acct_1", 10m, "idem-1");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Stripe não retornou dados Pix.");
    }

    [Fact]
    public async Task CriarPixPlataformaPaymentIntentAsync_SemDadosPix_LancaInvalidOperation()
    {
        var fake = new FakeStripeClient().Returns(PixIntentSemDados("pi_pix"));

        var acao = () => CriarServico(fake.Object).CriarPixPlataformaPaymentIntentAsync(100m, "idem-1");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Stripe não retornou dados Pix.");
    }

    [Fact]
    public async Task CriarPixPaymentIntentAsync_ComDadosPix_MapeiaQrCodeEExpiracaoDeterministica()
    {
        var fake = new FakeStripeClient().Returns(PixIntent("pi_pix"));

        var resultado = await CriarServico(fake.Object).CriarPixPaymentIntentAsync(100m, "acct_1", 10m, "idem-1");

        resultado.PaymentIntentId.Should().Be("pi_pix");
        resultado.QrCode.Should().Be("00020126pix-payload");
        resultado.QrCodeUrl.Should().Be("https://stripe.test/qr.png");
        resultado.Expiracao.Should().Be(Instante.UtcDateTime.AddSeconds(3600));
    }

    [Fact]
    public async Task CriarPixPlataformaPaymentIntentAsync_ComDadosPix_MapeiaQrCodeEExpiracaoDeterministica()
    {
        var fake = new FakeStripeClient().Returns(PixIntent("pi_pix"));

        var resultado = await CriarServico(fake.Object).CriarPixPlataformaPaymentIntentAsync(100m, "idem-1");

        resultado.PaymentIntentId.Should().Be("pi_pix");
        resultado.QrCode.Should().Be("00020126pix-payload");
        resultado.QrCodeUrl.Should().Be("https://stripe.test/qr.png");
        resultado.Expiracao.Should().Be(Instante.UtcDateTime.AddSeconds(3600));
    }

    [Fact]
    public async Task CriarCartaoPaymentIntentAsync_RetornaIdEClientSecret()
    {
        var fake = new FakeStripeClient()
            .Returns(new PaymentIntent { Id = "pi_card", ClientSecret = "pi_card_secret" });

        var resultado = await CriarServico(fake.Object).CriarCartaoPaymentIntentAsync(100m, "acct_1", 10m, "idem-1");

        resultado.PaymentIntentId.Should().Be("pi_card");
        resultado.ClientSecret.Should().Be("pi_card_secret");
    }

    [Fact]
    public async Task CriarCartaoPaymentIntentAsync_ConnectEnviaTaxaETransfer()
    {
        var fake = new FakeStripeClient();
        var (valorCentavos, taxaCentavos) = MoneyCentavos.ValorETaxaCentavos(100m, 10m);

        PaymentIntentCreateOptions? enviado = null;
        fake.Mock
            .Setup(c => c.RequestAsync<PaymentIntent>(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<BaseOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(inv => enviado = (PaymentIntentCreateOptions)inv.Arguments[2]))
            .ReturnsAsync(new PaymentIntent { Id = "pi_card", ClientSecret = "pi_card_secret" });

        await CriarServico(fake.Object).CriarCartaoPaymentIntentAsync(100m, "acct_1", 10m, "idem-1");

        enviado.Should().NotBeNull();
        enviado!.Amount.Should().Be(valorCentavos);
        enviado.ApplicationFeeAmount.Should().Be(taxaCentavos);
        enviado.TransferData!.Destination.Should().Be("acct_1");
    }

    [Fact]
    public async Task CriarCartaoPlataformaPaymentIntentAsync_MarcaMetadataESemTaxaOuTransfer()
    {
        var fake = new FakeStripeClient();

        PaymentIntentCreateOptions? enviado = null;
        fake.Mock
            .Setup(c => c.RequestAsync<PaymentIntent>(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<BaseOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(inv => enviado = (PaymentIntentCreateOptions)inv.Arguments[2]))
            .ReturnsAsync(new PaymentIntent { Id = "pi_card", ClientSecret = "pi_card_secret" });

        var resultado = await CriarServico(fake.Object).CriarCartaoPlataformaPaymentIntentAsync(100m, "idem-1");

        resultado.PaymentIntentId.Should().Be("pi_card");
        resultado.ClientSecret.Should().Be("pi_card_secret");
        enviado.Should().NotBeNull();
        enviado!.Metadata["tipo"].Should().Be("plano_treinador");
        enviado.ApplicationFeeAmount.Should().BeNull();
        enviado.TransferData.Should().BeNull();
    }
}
