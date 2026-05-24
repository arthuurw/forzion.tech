using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Planos.ListarPlanosPlataforma;

public class ListarPlanosPlataformaHandler(IPlanoPlataformaRepository planoRepository)
{
    public virtual async Task<IReadOnlyList<PlanoPlataformaResponse>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var planos = await planoRepository.ListarAsync(cancellationToken).ConfigureAwait(false);
        return planos.Select(PlanoPlataformaResponseExtensions.ToResponse).ToList();
    }
}
