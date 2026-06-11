using forzion.tech.Infrastructure.Outbox;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace forzion.tech.Api.Services;

// Host do worker outbox: cria um escopo por ciclo e delega a OutboxProcessor (lógica
// de lote/lease/retry). Mantém o host fino e a lógica testável fora do BackgroundService.
public sealed class OutboxProcessorService(
    IServiceProvider serviceProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessorService> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.IntervaloPolling, stoppingToken).ConfigureAwait(false);

                using var scope = serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                await processor.ProcessarLoteAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro no ciclo do worker outbox.");
            }
        }
    }
}
