using Stripe;

namespace forzion.tech.Infrastructure.Services;

public static class StripeClientFactory
{
    public static HttpClient CriarHttpClient(StripeSettings settings) =>
        new() { Timeout = TimeSpan.FromSeconds(settings.TimeoutSegundos) };

    public static StripeClient Construir(StripeSettings settings) =>
        new(
            settings.SecretKey,
            httpClient: new SystemNetHttpClient(
                CriarHttpClient(settings),
                maxNetworkRetries: settings.MaxNetworkRetries));
}
