using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Treinadores.Agenda;

public class ObterPoliticaAgendaHandler(ITreinadorRepository treinadorRepository)
{
    public virtual Task<Result<PoliticaAgendaResponse>> HandleAsync(
        Guid treinadorId,
        CancellationToken cancellationToken = default) =>
        HandleAsyncCore(treinadorId, cancellationToken);

    private async Task<Result<PoliticaAgendaResponse>> HandleAsyncCore(Guid treinadorId, CancellationToken cancellationToken)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
            return Result.Failure<PoliticaAgendaResponse>(TreinadorErrors.NaoEncontrado);

        return Result.Success(MapResponse(treinador.PoliticaAgenda));
    }

    internal static PoliticaAgendaResponse MapResponse(PoliticaAgenda politica) =>
        new(politica.AntecedenciaMinimaHoras, politica.HorizonteDias);
}
