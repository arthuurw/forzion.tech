using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Api.Services;

public class LimparTokensRevogadosService(
    IServiceProvider serviceProvider,
    ILogger<LimparTokensRevogadosService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);

                using var scope = serviceProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<ITokenRevogadoRepository>();
                var removed = await repo.LimparExpiradosAsync(stoppingToken).ConfigureAwait(false);

                if (removed > 0)
                    logger.LogInformation("Limpeza de tokens revogados: {Count} registros removidos.", removed);

                // GC das sessões de refresh: famílias revogadas ou além do teto absoluto.
                // Tokens (incl. usados, retidos p/ reuse-detection enquanto a família vive)
                // caem por cascade no nível do banco junto da família.
                var familyRepo = scope.ServiceProvider.GetRequiredService<IRefreshTokenFamilyRepository>();
                var agora = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                var familiasRemovidas = await familyRepo.LimparExpiradasAsync(agora, stoppingToken).ConfigureAwait(false);

                if (familiasRemovidas > 0)
                    logger.LogInformation("Limpeza de famílias de refresh: {Count} famílias removidas.", familiasRemovidas);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao limpar tokens revogados expirados.");
            }
        }
    }
}
