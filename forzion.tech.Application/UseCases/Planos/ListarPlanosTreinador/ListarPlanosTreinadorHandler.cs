using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Planos.ListarPlanosTreinador;

public class ListarPlanosTreinadorHandler(IPlanoTreinadorRepository planoRepository)
{
    public virtual async Task<IReadOnlyList<PlanoTreinadorResponse>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var planos = await planoRepository.ListarAsync(cancellationToken).ConfigureAwait(false);
        return planos.Select(PlanoTreinadorResponseExtensions.ToResponse).ToList();
    }
}
