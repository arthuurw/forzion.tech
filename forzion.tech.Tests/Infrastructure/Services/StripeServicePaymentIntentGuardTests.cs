using FluentAssertions;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Infrastructure.Services;

public class StripeServicePaymentIntentGuardTests
{
    private static StripeService Criar() =>
        new(Options.Create(new StripeSettings { SecretKey = "sk_test_x" }), TimeProvider.System, NullLogger<StripeService>.Instance);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CriarPixPaymentIntentAsync_ValorZeroOuNegativo_Lanca(decimal valor)
    {
        var svc = Criar();

        var act = () => svc.CriarPixPaymentIntentAsync(valor, "acct_x", 0.1m, "key");

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CriarCartaoPaymentIntentAsync_ValorZeroOuNegativo_Lanca(decimal valor)
    {
        var svc = Criar();

        var act = () => svc.CriarCartaoPaymentIntentAsync(valor, "acct_x", 0.1m, "key");

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CriarPixPlataformaPaymentIntentAsync_ValorZeroOuNegativo_Lanca(decimal valor)
    {
        var svc = Criar();

        var act = () => svc.CriarPixPlataformaPaymentIntentAsync(valor, "key");

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CriarCartaoPlataformaPaymentIntentAsync_ValorZeroOuNegativo_Lanca(decimal valor)
    {
        var svc = Criar();

        var act = () => svc.CriarCartaoPlataformaPaymentIntentAsync(valor, "key");

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
