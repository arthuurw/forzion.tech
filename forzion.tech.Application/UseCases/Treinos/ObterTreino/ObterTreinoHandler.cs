using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.ObterTreino;

public class ObterTreinoHandler(
    ITreinoRepository treinoRepository,
    IExercicioRepository exercicioRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IUserContext userContext,
    ILogger<ObterTreinoHandler> logger)
{
    public virtual Task<TreinoResponse> HandleAsync(
        ObterTreinoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<TreinoResponse> HandleAsyncCore(
        ObterTreinoQuery query,
        CancellationToken cancellationToken = default)
    {
        var treino = await treinoRepository
            .ObterPorIdAsync(query.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        // Validar autorização
        if (!userContext.IsSystemAdmin && treino.TreinadorId != userContext.PerfilId)
        {
            if (userContext.IsAluno)
            {
                var vinculo = await treinoAlunoRepository
                    .ObterAsync(treino.Id, userContext.PerfilId, cancellationToken)
                    .ConfigureAwait(false);

                if (vinculo is null)
                    throw new AcessoNegadoException();
            }
            else
            {
                throw new AcessoNegadoException();
            }
        }

        logger.LogInformation("Treino {TreinoId} consultado.", treino.Id);

        var nomesExercicio = await exercicioRepository
            .ObterNomesPorIdsAsync(treino.Exercicios.Select(e => e.ExercicioId), cancellationToken)
            .ConfigureAwait(false);

        return TreinoResponseExtensions.ToResponse(treino, nomesExercicio: nomesExercicio);
    }
}
