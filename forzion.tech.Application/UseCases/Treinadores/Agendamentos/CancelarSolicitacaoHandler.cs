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
    IDatabaseErrorInspector databaseErrorInspector,
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

        try
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        // O xmin de solicitacoes_agendamento (T8) aborta o UPDATE quando outra transição
        // (Confirmar/Recusar) já commitou nesta solicitação entre a leitura e este commit —
        // a decisão concorrente venceu, então a transição pedida aqui não se aplica mais.
        catch (Exception ex) when (databaseErrorInspector.EhConflitoDeConcorrenciaOtimista(ex))
        {
            unitOfWork.DescartarAlteracoesPendentes();
            return Result.Failure(SolicitacaoAgendamentoErrors.TransicaoNaoSuportada);
        }

        return Result.Success();
    }
}
