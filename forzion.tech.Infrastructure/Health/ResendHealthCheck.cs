using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace forzion.tech.Infrastructure.Health;

// Probe leve: GET /api-keys exige autenticação válida, sem side-effect e sem payload grande.
// Degraded em vez de Unhealthy — Resend fora do ar não impede leitura/treino, só envio de e-mail.
// Memoizado (janela curta): registrado como singleton em DI (AddSingleton em
// DependencyInjectionExtensions) para que os campos de cache sobrevivam entre invocações —
// AddCheck<T> sem registro próprio cria instância NOVA a cada /health/ready.
public sealed class ResendHealthCheck(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    TimeProvider timeProvider) : IHealthCheck
{
#pragma warning disable S1075 // Endpoint fixo da API do Resend (host público estável), não config de ambiente.
    private const string ApiKeysUrl = "https://api.resend.com/api-keys";
#pragma warning restore S1075
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MemoizeWindow = TimeSpan.FromSeconds(30);

    private readonly Lock _lock = new();
    private HealthCheckResult? _cached;
    private DateTimeOffset _cachedAt;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_cached is { } cached && timeProvider.GetUtcNow() - _cachedAt < MemoizeWindow)
                return cached;
        }

        var resultado = await ProbarAsync(cancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            _cached = resultado;
            _cachedAt = timeProvider.GetUtcNow();
        }

        return resultado;
    }

    private async Task<HealthCheckResult> ProbarAsync(CancellationToken cancellationToken)
    {
        var apiKey = configuration["Resend:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            return environment.IsProduction()
                ? HealthCheckResult.Unhealthy("Resend não configurado em produção — e-mail transacional está como no-op.")
                : HealthCheckResult.Healthy("Resend não configurado.");

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
