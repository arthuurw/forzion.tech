using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Leads.RegistrarInteracao;

public class RegistrarInteracaoLeadHandler(
    ILeadRepository leadRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result<RegistrarInteracaoLeadResponse>> HandleAsync(
        RegistrarInteracaoLeadCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<RegistrarInteracaoLeadResponse>> HandleAsyncCore(
        RegistrarInteracaoLeadCommand command,
        CancellationToken cancellationToken)
    {
        var lead = await leadRepository
            .ObterComHistoricoAsync(command.TreinadorId, command.LeadId, cancellationToken)
            .ConfigureAwait(false);
        if (lead is null)
            return Result.Failure<RegistrarInteracaoLeadResponse>(LeadErrors.NaoEncontrado);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var resultado = lead.RegistrarInteracao(command.RealizadoPorId, command.Observacao, agora);
        if (resultado.IsFailure)
            return Result.Failure<RegistrarInteracaoLeadResponse>(resultado.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new RegistrarInteracaoLeadResponse(lead.Id));
    }
}
