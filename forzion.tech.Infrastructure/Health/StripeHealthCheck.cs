using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Stripe;

namespace forzion.tech.Infrastructure.Health;

// Probe leve: lê o balance da conta plataforma (sem parâmetros, sem cursor, sem side-effect).
// Degraded em vez de Unhealthy — Stripe fora do ar não impede login/treino, só pagamentos.
public sealed class StripeHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
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
