using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Exercicios.ListarExercicios;

public class ListarExerciciosHandler(
    IExercicioRepository exercicioRepository,
    ILogger<ListarExerciciosHandler> logger)
{
    public virtual Task<ListarExerciciosResponse> HandleAsync(
        ListarExerciciosQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<ListarExerciciosResponse> HandleAsyncCore(
        ListarExerciciosQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await exercicioRepository
            .ListarAsync(query.TreinadorId, query.Pagina, query.TamanhoPagina, cancellationToken,
                query.Nome, query.GrupoMuscular, query.OrdenarPor)
            .ConfigureAwait(false);

        logger.LogInformation("Listagem de exercícios: {Total} registros.", total);

        return new ListarExerciciosResponse(
            items.Select(ExercicioResponseExtensions.ToResponse).ToList(),
            total,
            query.Pagina,
            query.TamanhoPagina);
    }
}
