using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Pacotes.ListarPacotes;

public class ListarPacotesHandler(IPacoteRepository pacoteRepository)
{
    public virtual async Task<IReadOnlyList<PacoteResponse>> HandleAsync(
        Guid treinadorId,
        CancellationToken cancellationToken = default)
    {
        var pacotes = await pacoteRepository.ListarPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        return pacotes.Select(PacoteResponseExtensions.ToResponse).ToList();
    }
}
