using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace forzion.tech.Infrastructure.Health;

// Probe leve: GET /api-keys exige autenticação válida, sem side-effect e sem payload grande.
// Degraded em vez de Unhealthy — Resend fora do ar não impede leitura/treino, só envio de e-mail.
public sealed class ResendHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IHealthCheck
{
    private const string ApiKeysUrl = "https://api.resend.com/api-keys";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Resend:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            return HealthCheckResult.Healthy("Resend não configurado.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);

        try
        {
            // Client de IHttpClientFactory não é descartado: a factory gerencia o lifetime do
            // handler (descartar interfere no pool). Só request/response liberam recursos próprios.
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, ApiKeysUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);

            // Sucesso apenas com 2xx. Outro status, como 401/403 de chave inválida, retorna
            // Degraded — Resend respondeu, mas o envio falharia. Exceção ou timeout é falha de rede.
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"Resend retornou {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded("Resend: timeout (3s).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"Resend: {ex.Message}");
        }
    }
}
