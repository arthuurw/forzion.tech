using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Treinadores.Agendamentos;

// Cancelar não roda em tx Serializable: libera capacidade, não a consome — sem invariante
// concorrente a proteger aqui (o coração da concorrência é ConfirmarSolicitacaoHandler).
public class CancelarSolicitacaoHandler(
    ISolicitacaoAgendamentoRepository solicitacaoAgendamentoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result> HandleAsync(Guid treinadorId, Guid solicitacaoId, string? motivo, CancellationToken cancellationToken = default) =>
        HandleAsyncCore(treinadorId, solicitacaoId, motivo, cancellationToken);

    private async Task<Result> HandleAsyncCore(Guid treinadorId, Guid solicitacaoId, string? motivo, CancellationToken cancellationToken)
    {
        var solicitacao = await solicitacaoAgendamentoRepository
            .ObterPorIdAsync(solicitacaoId, treinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (solicitacao is null)
            return Result.Failure(SolicitacaoAgendamentoErrors.NaoEncontrada);

        var cancelarResult = solicitacao.Cancelar(treinadorId, motivo, timeProvider.GetUtcNow().UtcDateTime);
        if (cancelarResult.IsFailure)
            return cancelarResult;

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
