using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Api.Services;

public class LimparTokensRevogadosService(
    IServiceProvider serviceProvider,
    ILogger<LimparTokensRevogadosService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Purga já no boot (antes do delay): instância que reinicia em <1h (deploy/scale) nunca
        // chegaria à 1ª limpeza, deixando refresh_tokens/famílias crescer sem teto.
        while (!stoppingToken.IsCancellationRequested)
        {
            await LimparAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task LimparAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();

        // Cada purga em try/catch próprio: são independentes, falha numa não pode pular a outra.
        try
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITokenRevogadoRepository>();
            var removed = await repo.LimparExpiradosAsync(stoppingToken).ConfigureAwait(false);
            if (removed > 0)
                logger.LogInformation("Limpeza de tokens revogados: {Count} registros removidos.", removed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro ao limpar tokens revogados expirados.");
        }

        try
        {
            // Famílias revogadas ou além do teto absoluto. Os tokens (incl. usados, retidos p/
            // reuse-detection enquanto a família vive) caem por cascade no nível do banco.
            var familyRepo = scope.ServiceProvider.GetRequiredService<IRefreshTokenFamilyRepository>();
            var agora = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var familiasRemovidas = await familyRepo.LimparExpiradasAsync(agora, stoppingToken).ConfigureAwait(false);
            if (familiasRemovidas > 0)
                logger.LogInformation("Limpeza de famílias de refresh: {Count} famílias removidas.", familiasRemovidas);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro ao limpar famílias de refresh expiradas.");
        }

        try
        {
            var challengeRepo = scope.ServiceProvider.GetRequiredService<IMfaChallengeRepository>();
            var desafiosRemovidos = await challengeRepo.LimparExpiradosAsync(stoppingToken).ConfigureAwait(false);
            if (desafiosRemovidos > 0)
                logger.LogInformation("Limpeza de desafios MFA: {Count} registros removidos.", desafiosRemovidos);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro ao limpar desafios MFA expirados.");
        }

        try
        {
            var deviceRepo = scope.ServiceProvider.GetRequiredService<ITrustedDeviceRepository>();
            var dispositivosRemovidos = await deviceRepo.LimparExpiradosAsync(stoppingToken).ConfigureAwait(false);
            if (dispositivosRemovidos > 0)
                logger.LogInformation("Limpeza de dispositivos confiáveis: {Count} registros removidos.", dispositivosRemovidos);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro ao limpar dispositivos confiáveis expirados.");
        }

        try
        {
            var errorLogRepo = scope.ServiceProvider.GetRequiredService<IErrorLogRepository>();
            var logsRemovidos = await errorLogRepo.LimparAntigosAsync(stoppingToken).ConfigureAwait(false);
            if (logsRemovidos > 0)
                logger.LogInformation("Limpeza de error_logs (retenção 90d): {Count} registros removidos.", logsRemovidos);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro ao limpar error_logs além da retenção.");
        }
    }
}
