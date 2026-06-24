using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace forzion.tech.Infrastructure.Health;

public sealed class WhatsAppHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var phoneNumberId = configuration["WhatsApp:PhoneNumberId"];
        var accessToken = configuration["WhatsApp:AccessToken"];

        if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(accessToken))
            return HealthCheckResult.Healthy("WhatsApp não configurado.");

        var version = configuration["WhatsApp:ApiVersion"] ?? "v21.0";
        var url = $"https://graph.facebook.com/{version}/{phoneNumberId}?fields=id";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);

        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"WhatsApp retornou {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded("WhatsApp: timeout (3s).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"WhatsApp: {ex.Message}");
        }
    }
}
