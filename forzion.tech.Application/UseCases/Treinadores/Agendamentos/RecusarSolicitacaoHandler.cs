using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Treinadores.Agendamentos;

public class RecusarSolicitacaoHandler(
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

        var recusarResult = solicitacao.Recusar(treinadorId, motivo, timeProvider.GetUtcNow().UtcDateTime);
        if (recusarResult.IsFailure)
            return recusarResult;

        try
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        // O xmin de solicitacoes_agendamento (T8) aborta o UPDATE quando outra transição
        // (Confirmar/Cancelar) já commitou nesta solicitação entre a leitura e este commit —
        // a decisão concorrente venceu, então a transição pedida aqui não se aplica mais.
        catch (Exception ex) when (databaseErrorInspector.EhConflitoDeConcorrenciaOtimista(ex))
        {
            unitOfWork.DescartarAlteracoesPendentes();
            return Result.Failure(SolicitacaoAgendamentoErrors.TransicaoNaoSuportada);
        }

        return Result.Success();
    }
}
