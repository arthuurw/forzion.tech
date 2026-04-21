using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.ListarGruposMusculares;

public class ListarGruposMuscularesHandler(IGrupoMuscularRepository repository)
{
    private readonly IGrupoMuscularRepository _repository = repository;

    public async Task<IReadOnlyList<GrupoMuscularResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var grupos = await _repository.ListarTodosAsync(cancellationToken);
        
        return grupos.Select(g => new GrupoMuscularResponse(g.Id, g.Nome, g.CreatedAt, g.UpdatedAt)).ToList();
    }
}
