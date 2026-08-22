using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Api.Services;

public class LimparTokensRevogadosService(
    IServiceProvider serviceProvider,
    ILogger<LimparTokensRevogadosService> logger) : BackgroundService
{
    private const int RetencaoNotificacaoDias = 90;
    private const int RetencaoLeadSemToqueDias = 180;

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
            var trocaEmailTokenRepo = scope.ServiceProvider.GetRequiredService<ITrocaEmailTokenRepository>();
            var trocaEmailTokensRemovidos = await trocaEmailTokenRepo.LimparExpiradosAsync(stoppingToken).ConfigureAwait(false);
            if (trocaEmailTokensRemovidos > 0)
                logger.LogInformation("Limpeza de tokens de troca de e-mail: {Count} registros removidos.", trocaEmailTokensRemovidos);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro ao limpar tokens de troca de e-mail expirados.");
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

        try
        {
            var notificacaoRepo = scope.ServiceProvider.GetRequiredService<INotificacaoRepository>();
            var agora = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var limite = agora.AddDays(-RetencaoNotificacaoDias);
            var notificacoesRemovidas = await notificacaoRepo.PurgarAntesDeAsync(limite, stoppingToken).ConfigureAwait(false);
            if (notificacoesRemovidas > 0)
                logger.LogInformation("Limpeza de notificações (retenção {Dias}d): {Count} registros removidos.", RetencaoNotificacaoDias, notificacoesRemovidas);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro ao limpar notificações além da retenção.");
        }

        try
        {
            var leadRepo = scope.ServiceProvider.GetRequiredService<ILeadRepository>();
            var agora = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var cutoff = agora.AddDays(-RetencaoLeadSemToqueDias);
            var leadsAnonimizados = await leadRepo.AnonimizarInativosAsync(cutoff, agora, stoppingToken).ConfigureAwait(false);
            if (leadsAnonimizados > 0)
                logger.LogInformation("Anonimização de leads (retenção {Dias}d sem toque): {Count} registros.", RetencaoLeadSemToqueDias, leadsAnonimizados);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro ao anonimizar leads além da retenção.");
        }

        try
        {
            var conviteRepo = scope.ServiceProvider.GetRequiredService<ILeadConviteRepository>();
            var agora = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var convitesRemovidos = await conviteRepo.LimparExpiradosOuConsumidosAsync(agora, stoppingToken).ConfigureAwait(false);
            if (convitesRemovidos > 0)
                logger.LogInformation("Limpeza de convites de lead expirados/consumidos: {Count} registros removidos.", convitesRemovidos);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erro ao limpar convites de lead expirados/consumidos.");
        }
    }
}
