using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Stripe;

namespace forzion.tech.Tests.Infrastructure.Services;

public class StripeServiceContaReconciliacaoTests
{
    private static StripeService CriarServico(IStripeClient client, int maxEventos = 1000) =>
        new(
            Options.Create(new StripeSettings { SecretKey = "sk_test_x", MaxEventosReconciliacaoPorRun = maxEventos }),
            TimeProvider.System,
            NullLogger<StripeService>.Instance,
            client);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ContaEstaAtivadaAsync_RetornaChargesEnabledDaConta(bool chargesEnabled)
    {
        var client = new FakeStripeClient().Returns(new Account { Id = "acct_1", ChargesEnabled = chargesEnabled });

        var ativada = await CriarServico(client.Object).ContaEstaAtivadaAsync("acct_1");

        ativada.Should().Be(chargesEnabled);
    }

    [Fact]
    public async Task CriarReembolsoAsync_ReverterTransferencia_MarcaReverseTransferERefundApplicationFee()
    {
        var fake = new FakeStripeClient();
        var enviado = CapturarRefundOptions(fake);

        await CriarServico(fake.Object).CriarReembolsoAsync(Guid.NewGuid(), "pi_1", reverterTransferencia: true);

        enviado().ReverseTransfer.Should().Be(true);
        enviado().RefundApplicationFee.Should().Be(true);
    }

    [Fact]
    public async Task CriarReembolsoAsync_SemReverterTransferencia_NaoMarcaReverseNemRefundFeeEUsaPaymentIntent()
    {
        var fake = new FakeStripeClient();
        var enviado = CapturarRefundOptions(fake);

        await CriarServico(fake.Object).CriarReembolsoAsync(Guid.NewGuid(), "pi_1", reverterTransferencia: false);

        enviado().ReverseTransfer.Should().BeNull();
        enviado().RefundApplicationFee.Should().BeNull();
        enviado().PaymentIntent.Should().Be("pi_1");
    }

    [Fact]
    public async Task EnviarEvidenciaDisputaAsync_EvidenciasNulas_LancaArgumentNull()
    {
        var acao = () => CriarServico(new FakeStripeClient().Object).EnviarEvidenciaDisputaAsync("dp_1", null!);

        await acao.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnviarEvidenciaDisputaAsync_TodasAsDatas_MontaTextoEServiceDate()
    {
        var fake = new FakeStripeClient();
        var enviado = CapturarDisputeOptions(fake);
        var evidencias = new DisputaEvidencia(
            EmailCliente: "cliente@teste.com",
            DataAtivacao: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            DataUltimaAtividade: new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc),
            DataUltimoPagamento: new DateTime(2026, 5, 6, 0, 0, 0, DateTimeKind.Utc));

        await CriarServico(fake.Object).EnviarEvidenciaDisputaAsync("dp_1", evidencias);

        enviado().Evidence.UncategorizedText.Should().Be(
            "Serviço ativado em 2026-01-02. Última atividade do cliente em 2026-03-04. Último pagamento confirmado em 2026-05-06.");
        enviado().Evidence.ServiceDate.Should().Be("2026-01-02");
    }

    [Fact]
    public async Task EnviarEvidenciaDisputaAsync_SomenteDataAtivacao_TextoContemApenasPrimeiraFrase()
    {
        var fake = new FakeStripeClient();
        var enviado = CapturarDisputeOptions(fake);
        var evidencias = new DisputaEvidencia(
            EmailCliente: null,
            DataAtivacao: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            DataUltimaAtividade: null,
            DataUltimoPagamento: null);

        await CriarServico(fake.Object).EnviarEvidenciaDisputaAsync("dp_1", evidencias);

        enviado().Evidence.UncategorizedText.Should().Be("Serviço ativado em 2026-01-02.");
    }

    [Fact]
    public async Task ListarEventosDesdeAsync_AbaixoDoTeto_OrdenaAscendentePorCreatedESemTruncar()
    {
        var maisNovo = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc);
        var maisAntigo = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var pagina = new StripeList<Event>
        {
            HasMore = false,
            Data =
            [
                new Event { Id = "evt_2", Type = "payment_intent.succeeded", Created = maisNovo },
                new Event { Id = "evt_1", Type = "payment_intent.succeeded", Created = maisAntigo },
            ],
        };
        var fake = new FakeStripeClient().Returns(pagina);

        var resultado = await CriarServico(fake.Object).ListarEventosDesdeAsync(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        resultado.Truncado.Should().BeFalse();
        resultado.Eventos.Select(e => e.Created).Should().ContainInOrder(maisAntigo, maisNovo);
        resultado.Eventos.Select(e => e.Created).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ListarEventosDesdeAsync_EnviaTiposReconciliaveisLimiteEIntervalo()
    {
        var fake = new FakeStripeClient();
        var enviado = CapturarEventListOptions(fake, new StripeList<Event> { HasMore = false, Data = [] });
        var desde = new DateTime(2026, 6, 1, 8, 30, 0, DateTimeKind.Utc);

        await CriarServico(fake.Object).ListarEventosDesdeAsync(desde);

        enviado().Types.Should().Equal(
            "payment_intent.succeeded",
            "payment_intent.payment_failed",
            "payment_intent.canceled",
            "account.updated",
            "charge.refunded",
            "charge.dispute.created");
        enviado().Limit.Should().Be(100);
        var intervalo = (DateRangeOptions)enviado().Created;
        intervalo.GreaterThanOrEqual.Should().Be(desde);
        ((DateTime)intervalo.GreaterThanOrEqual!).Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ListarEventosDesdeAsync_NoTeto_ColetaAteOCapESinalizaTruncado()
    {
        var pagina = new StripeList<Event>
        {
            HasMore = false,
            Data =
            [
                new Event { Id = "evt_3", Type = "payment_intent.succeeded", Created = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc) },
                new Event { Id = "evt_2", Type = "payment_intent.succeeded", Created = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc) },
                new Event { Id = "evt_1", Type = "payment_intent.succeeded", Created = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
            ],
        };
        var fake = new FakeStripeClient().Returns(pagina);

        var resultado = await CriarServico(fake.Object, maxEventos: 2).ListarEventosDesdeAsync(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        resultado.Truncado.Should().BeTrue();
        resultado.Eventos.Select(e => e.EventId).Should().Equal("evt_1", "evt_2");
    }

    [Fact]
    public async Task ListarEventosDesdeAsync_TokenCancelado_Lanca()
    {
        var pagina = new StripeList<Event>
        {
            HasMore = false,
            Data = [new Event { Id = "evt_1", Type = "payment_intent.succeeded", Created = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc) }],
        };
        var fake = new FakeStripeClient().Returns(pagina);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var acao = () => CriarServico(fake.Object).ListarEventosDesdeAsync(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), cts.Token);

        await acao.Should().ThrowAsync<OperationCanceledException>();
    }

    private static Func<RefundCreateOptions> CapturarRefundOptions(FakeStripeClient fake)
    {
        RefundCreateOptions? capturado = null;
        fake.Mock
            .Setup(c => c.RequestAsync<Refund>(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<BaseOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(inv => capturado = (RefundCreateOptions)inv.Arguments[2]))
            .ReturnsAsync(new Refund { Id = "re_x" });
        return () => capturado!;
    }

    private static Func<DisputeUpdateOptions> CapturarDisputeOptions(FakeStripeClient fake)
    {
        DisputeUpdateOptions? capturado = null;
        fake.Mock
            .Setup(c => c.RequestAsync<Dispute>(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<BaseOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(inv => capturado = (DisputeUpdateOptions)inv.Arguments[2]))
            .ReturnsAsync(new Dispute { Id = "dp_x" });
        return () => capturado!;
    }

    private static Func<EventListOptions> CapturarEventListOptions(FakeStripeClient fake, StripeList<Event> pagina)
    {
        EventListOptions? capturado = null;
        fake.Mock
            .Setup(c => c.RequestAsync<StripeList<Event>>(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<BaseOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(inv => capturado = (EventListOptions)inv.Arguments[2]))
            .ReturnsAsync(pagina);
        return () => capturado!;
    }
}
