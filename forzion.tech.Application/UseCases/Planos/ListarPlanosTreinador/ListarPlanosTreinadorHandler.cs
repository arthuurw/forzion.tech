using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Planos.ListarPlanosTreinador;

public class ListarPlanosTreinadorHandler(IPlanoTreinadorRepository planoRepository)
{
    private readonly IPlanoTreinadorRepository _planoRepository = planoRepository;

    public virtual async Task<IReadOnlyList<PlanoTreinadorResponse>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var planos = await _planoRepository.ListarAsync(cancellationToken).ConfigureAwait(false);
        return planos.Select(PlanoTreinadorResponseExtensions.ToResponse).ToList();
    }
}
