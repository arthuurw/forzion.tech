using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Treinadores.Agenda;

public class ListarBloqueiosAgendaHandler(IBloqueioAgendaRepository bloqueioAgendaRepository)
{
    public virtual Task<IReadOnlyList<BloqueioAgendaResponse>> HandleAsync(
        Guid treinadorId,
        CancellationToken cancellationToken = default) =>
        HandleAsyncCore(treinadorId, cancellationToken);

    private async Task<IReadOnlyList<BloqueioAgendaResponse>> HandleAsyncCore(Guid treinadorId, CancellationToken cancellationToken)
    {
        var bloqueios = await bloqueioAgendaRepository.ListarPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        return [.. bloqueios.Select(CriarBloqueioAgendaHandler.MapResponse)];
    }
}
