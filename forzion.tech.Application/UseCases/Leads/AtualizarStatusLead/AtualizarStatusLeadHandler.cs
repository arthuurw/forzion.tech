using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Leads.AtualizarStatusLead;

public class AtualizarStatusLeadHandler(
    ILeadRepository leadRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result<AtualizarStatusLeadResponse>> HandleAsync(
        AtualizarStatusLeadCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<AtualizarStatusLeadResponse>> HandleAsyncCore(
        AtualizarStatusLeadCommand command,
        CancellationToken cancellationToken)
    {
        var lead = await leadRepository
            .ObterComHistoricoAsync(command.TreinadorId, command.LeadId, cancellationToken)
            .ConfigureAwait(false);
        if (lead is null)
            return Result.Failure<AtualizarStatusLeadResponse>(LeadErrors.NaoEncontrado);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var transicao = command.NovoStatus switch
        {
            LeadStatus.EmContato => lead.MarcarEmContato(command.RealizadoPorId, command.Observacao, agora),
            LeadStatus.Descartado => command.Motivo is null
                ? Result.Failure(LeadErrors.MotivoDescarteObrigatorio)
                : lead.Descartar(command.Motivo.Value, command.RealizadoPorId, command.Observacao, agora),
            _ => Result.Failure(LeadErrors.TransicaoNaoSuportada),
        };
        if (transicao.IsFailure)
            return Result.Failure<AtualizarStatusLeadResponse>(transicao.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new AtualizarStatusLeadResponse(lead.Id, lead.Status));
    }
}
