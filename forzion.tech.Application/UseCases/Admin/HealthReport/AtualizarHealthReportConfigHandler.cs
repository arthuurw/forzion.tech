using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public class AtualizarHealthReportConfigHandler(
    IHealthReportConfigRepository configRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<AtualizarHealthReportConfigCommand> validator)
{
    public virtual Task<HealthReportConfigResponse> HandleAsync(
        AtualizarHealthReportConfigCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<HealthReportConfigResponse> HandleAsyncCore(
        AtualizarHealthReportConfigCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var destinatarios = command.Destinatarios ?? Array.Empty<string>();
        var config = await configRepository.ObterAsync(cancellationToken).ConfigureAwait(false);

        if (config is null)
        {
            config = HealthReportConfig.Criar(
                command.Ativo,
                command.HoraEnvioUtc,
                destinatarios,
                command.IncluirLiveness,
                command.IncluirKpis,
                command.IncluirEntregabilidade,
                command.IncluirErros,
                timeProvider.GetUtcNow().UtcDateTime);
            await configRepository.AdicionarAsync(config, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            config.Atualizar(
                command.Ativo,
                command.HoraEnvioUtc,
                destinatarios,
                command.IncluirLiveness,
                command.IncluirKpis,
                command.IncluirEntregabilidade,
                command.IncluirErros);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return HealthReportConfigResponseExtensions.ToResponse(config);
    }
}
