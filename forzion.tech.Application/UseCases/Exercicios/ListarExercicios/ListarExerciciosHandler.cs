using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Exercicios.ListarExercicios;

public class ListarExerciciosHandler(
    IExercicioRepository exercicioRepository,
    ILogger<ListarExerciciosHandler> logger)
{
    private readonly IExercicioRepository _exercicioRepository = exercicioRepository;
    private readonly ILogger<ListarExerciciosHandler> _logger = logger;

    public virtual async Task<ListarExerciciosResponse> HandleAsync(
        ListarExerciciosQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (items, total) = await _exercicioRepository
            .ListarAsync(query.TreinadorId, query.Pagina, query.TamanhoPagina, cancellationToken,
                query.Nome, query.GrupoMuscular, query.OrdenarPor)
            .ConfigureAwait(false);

        _logger.LogInformation("Listagem de exercícios: {Total} registros.", total);

        return new ListarExerciciosResponse(
            items.Select(ExercicioResponseExtensions.ToResponse).ToList(),
            total,
            query.Pagina,
            query.TamanhoPagina);
    }
}
