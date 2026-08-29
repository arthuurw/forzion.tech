using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Stripe;

namespace forzion.tech.Infrastructure.Health;

// Probe leve: lê o balance da conta plataforma (sem parâmetros, sem cursor, sem side-effect).
// Degraded em vez de Unhealthy — Stripe fora do ar não impede login/treino, só pagamentos.
// Memoizado (janela curta): registrado como singleton em DI (AddSingleton em
// DependencyInjectionExtensions) para que os campos de cache sobrevivam entre invocações —
// AddCheck<T> sem registro próprio cria instância NOVA a cada /health/ready.
public sealed class StripeHealthCheck(IConfiguration configuration, TimeProvider timeProvider) : IHealthCheck
{
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
        var secretKey = configuration["Stripe:SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey))
            return HealthCheckResult.Healthy("Stripe não configurado.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);

        try
        {
            var service = new BalanceService();
            await service.GetAsync(
                requestOptions: new RequestOptions { ApiKey = secretKey },
                cancellationToken: cts.Token)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded("Stripe: timeout (3s).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"Stripe: {ex.Message}");
        }
    }
}
