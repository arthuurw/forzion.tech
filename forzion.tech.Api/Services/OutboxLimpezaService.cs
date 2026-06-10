using forzion.tech.Infrastructure.Outbox;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace forzion.tech.Api.Services;

// Cadência separada do worker (que faz polling a cada poucos segundos): a limpeza de
// itens Concluido só precisa rodar esporadicamente, então não onera cada ciclo de despacho.
public sealed class OutboxLimpezaService(
    IServiceProvider serviceProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxLimpezaService> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.IntervaloLimpeza, stoppingToken).ConfigureAwait(false);

                using var scope = serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                var removidos = await processor.LimparConcluidosAsync(stoppingToken).ConfigureAwait(false);

                if (removidos > 0)
                    logger.LogInformation("Limpeza do outbox: {Count} efeitos concluídos removidos.", removidos);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro na limpeza de efeitos concluídos do outbox.");
            }
        }
    }
}
