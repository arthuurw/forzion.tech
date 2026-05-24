using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Exercicios.ListarExercicios;

public class ListarExerciciosHandler(
    IExercicioRepository exercicioRepository,
    IGrupoMuscularRepository grupoMuscularRepository,
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
                query.Nome, query.GrupoMuscularId, query.OrdenarPor)
            .ConfigureAwait(false);

        var gruposNome = (await grupoMuscularRepository.ListarTodosAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(g => g.Id, g => g.Nome);

        logger.LogInformation("Listagem de exercícios: {Total} registros.", total);

        return new ListarExerciciosResponse(
            items.Select(e => ExercicioResponseExtensions.ToResponse(
                e, gruposNome.GetValueOrDefault(e.GrupoMuscularId, string.Empty))).ToList(),
            total,
            query.Pagina,
            query.TamanhoPagina);
    }
}
