using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Admin.Leads;

public record AnonimizarLeadCommand(Guid LeadId, Guid AdminId);

public class AnonimizarLeadHandler(
    ILeadRepository leadRepository,
    ILogAprovacaoRepository logAprovacaoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result> HandleAsync(
        AnonimizarLeadCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        AnonimizarLeadCommand command,
        CancellationToken cancellationToken)
    {
        var lead = await leadRepository
            .ObterPorIdCrossTenantAsync(command.LeadId, cancellationToken)
            .ConfigureAwait(false);
        if (lead is null)
            return Result.Failure(LeadErrors.NaoEncontrado);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        lead.Anonimizar(agora);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AnonimizacaoLead,
            command.AdminId,
            lead.Id,
            nameof(Lead),
            agora);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);

        await logAprovacaoRepository.AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
