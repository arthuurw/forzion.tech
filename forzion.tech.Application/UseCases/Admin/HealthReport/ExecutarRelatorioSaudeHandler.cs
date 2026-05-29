using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public class ExecutarRelatorioSaudeHandler(
    IHealthReportConfigRepository configRepository,
    IHealthReportCollector collector,
    IHealthSnapshotRepository snapshotRepository,
    IHealthReportSender sender,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual async Task<HealthSnapshotResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var config = await configRepository.ObterAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Configuração de relatório de saúde não encontrada.");

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var report = await collector.ColetarAsync(config, cancellationToken).ConfigureAwait(false);

        var snapshotResult = HealthSnapshot.Criar(report.Ambiente, report.StatusGeral, HealthReportPayload.Serializar(report), agora);
        if (snapshotResult.IsFailure)
            throw new DomainException(snapshotResult.Error!.Message);
        var snapshot = snapshotResult.Value;

        await snapshotRepository.AdicionarAsync(snapshot, cancellationToken).ConfigureAwait(false);

        await sender.EnviarAsync(report, config.ObterDestinatarios(), cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return HealthSnapshotResponseExtensions.ToResponse(snapshot);
    }
}
