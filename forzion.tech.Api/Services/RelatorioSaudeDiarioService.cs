using System.Text.Json;
using System.Text.Json.Serialization;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Api.Services;

public class RelatorioSaudeDiarioService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<RelatorioSaudeDiarioService> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool DeveEnviar(HealthReportConfig config, DateTime agoraUtc)
    {
        if (!config.Ativo)
            return false;
        if (TimeOnly.FromDateTime(agoraUtc) < config.HoraEnvioUtc)
            return false;
        if (config.UltimoEnvioEm is { } ultimo && ultimo.Date == agoraUtc.Date)
            return false;

        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Intervalo, stoppingToken).ConfigureAwait(false);
                await ProcessarAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro no relatório de saúde diário.");
            }
        }
    }

    private async Task ProcessarAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var configRepo = sp.GetRequiredService<IHealthReportConfigRepository>();
        var config = await configRepo.ObterAsync(cancellationToken).ConfigureAwait(false);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        if (config is null || !DeveEnviar(config, agora))
            return;

        var collector = sp.GetRequiredService<IHealthReportCollector>();
        var snapshotRepo = sp.GetRequiredService<IHealthSnapshotRepository>();
        var sender = sp.GetRequiredService<IHealthReportSender>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();

        var report = await collector.ColetarAsync(config, cancellationToken).ConfigureAwait(false);
        var payload = JsonSerializer.Serialize(report, JsonOptions);
        var snapshot = HealthSnapshot.Criar(report.Ambiente, report.StatusGeral, payload, agora);
        await snapshotRepo.AdicionarAsync(snapshot, cancellationToken).ConfigureAwait(false);

        await sender.EnviarAsync(report, config.ObterDestinatarios(), cancellationToken).ConfigureAwait(false);

        config.MarcarEnviado(agora);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Relatório de saúde enviado para {Total} destinatário(s) (status {Status}).",
            config.ObterDestinatarios().Count, report.StatusGeral);
    }
}
