using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public class ExecutarRelatorioSaudeHandler(
    IHealthReportConfigRepository configRepository,
    IHealthReportCollector collector,
    IHealthSnapshotRepository snapshotRepository,
    IHealthReportSender sender,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ExecutarRelatorioSaudeHandler> logger)
{
    public virtual async Task<Result<HealthSnapshotResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var config = await configRepository.ObterAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new EstadoInconsistenteException("Configuração de relatório de saúde não encontrada.");

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var report = await collector.ColetarAsync(config, cancellationToken).ConfigureAwait(false);

        var snapshotResult = HealthSnapshot.Criar(report.Ambiente, report.StatusGeral, HealthReportPayload.Serializar(report), agora);
        if (snapshotResult.IsFailure)
            return Result.Failure<HealthSnapshotResponse>(snapshotResult.Error!);
        var snapshot = snapshotResult.Value;

        await snapshotRepository.AdicionarAsync(snapshot, cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await sender.EnviarAsync(report, config.ObterDestinatarios(), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Relatório de saúde persistido, mas o envio de e-mail falhou.");
        }

        return Result.Success(HealthSnapshotResponseExtensions.ToResponse(snapshot));
    }
}
