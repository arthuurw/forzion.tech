using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public class AtualizarHealthReportConfigHandler(
    IHealthReportConfigRepository configRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<AtualizarHealthReportConfigCommand> validator)
{
    public virtual Task<Result<HealthReportConfigResponse>> HandleAsync(
        AtualizarHealthReportConfigCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<HealthReportConfigResponse>> HandleAsyncCore(
        AtualizarHealthReportConfigCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var destinatarios = command.Destinatarios ?? Array.Empty<string>();
        var config = await configRepository.ObterAsync(cancellationToken).ConfigureAwait(false);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (config is null)
        {
            var configResult = HealthReportConfig.Criar(
                command.Ativo,
                command.HoraEnvioUtc,
                destinatarios,
                command.IncluirLiveness,
                command.IncluirKpis,
                command.IncluirEntregabilidade,
                command.IncluirErros,
                agora);
            if (configResult.IsFailure)
                return Result.Failure<HealthReportConfigResponse>(configResult.Error!);
            config = configResult.Value;

            await configRepository.AdicionarAsync(config, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var atualizarResult = config.Atualizar(
                command.Ativo,
                command.HoraEnvioUtc,
                destinatarios,
                command.IncluirLiveness,
                command.IncluirKpis,
                command.IncluirEntregabilidade,
                command.IncluirErros,
                agora);
            if (atualizarResult.IsFailure)
                return Result.Failure<HealthReportConfigResponse>(atualizarResult.Error!);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(HealthReportConfigResponseExtensions.ToResponse(config));
    }
}
