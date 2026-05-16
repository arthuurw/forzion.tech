using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.ListarTreinos;

public class ListarTreinosHandler(
    ITreinoRepository treinoRepository,
    IExercicioRepository exercicioRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUserContext userContext,
    ILogger<ListarTreinosHandler> logger)
{
    public virtual Task<ListarTreinosResponse> HandleAsync(
        ListarTreinosQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<ListarTreinosResponse> HandleAsyncCore(
        ListarTreinosQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!userContext.IsSystemAdmin)
        {
            if (userContext.IsAluno)
            {
                if (userContext.PerfilId != query.AlunoId)
                    throw new AcessoNegadoException();
            }
            else
            {
                var vinculo = await vinculoRepository
                    .ObterAtivoAsync(userContext.PerfilId, query.AlunoId, cancellationToken)
                    .ConfigureAwait(false);

                if (vinculo is null)
                    throw new AcessoNegadoException();
            }
        }

        var (items, total) = await treinoRepository
            .ListarPorAlunoAsync(query.AlunoId, query.Pagina, query.TamanhoPagina, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Listagem de treinos do aluno {AlunoId}: {Total} registros.", query.AlunoId, total);

        var allExercicioIds = items.SelectMany(t => t.Exercicios.Select(e => e.ExercicioId));
        var nomesExercicio = await exercicioRepository
            .ObterNomesPorIdsAsync(allExercicioIds, cancellationToken)
            .ConfigureAwait(false);

        return new ListarTreinosResponse(
            items.Select(t => TreinoResponseExtensions.ToResponse(t, nomesExercicio: nomesExercicio)).ToList(),
            total,
            query.Pagina,
            query.TamanhoPagina);
    }
}
