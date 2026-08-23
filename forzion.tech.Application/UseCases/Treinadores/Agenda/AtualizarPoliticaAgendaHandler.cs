using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Treinadores.Agenda;

public class AtualizarPoliticaAgendaHandler(
    ITreinadorRepository treinadorRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result<PoliticaAgendaResponse>> HandleAsync(
        AtualizarPoliticaAgendaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<PoliticaAgendaResponse>> HandleAsyncCore(
        AtualizarPoliticaAgendaCommand command,
        CancellationToken cancellationToken)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
            return Result.Failure<PoliticaAgendaResponse>(TreinadorErrors.NaoEncontrado);

        var politicaResult = PoliticaAgenda.Criar(command.AntecedenciaMinimaHoras, command.HorizonteDias);
        if (politicaResult.IsFailure)
            return Result.Failure<PoliticaAgendaResponse>(politicaResult.Error!);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var definirResult = treinador.DefinirPoliticaAgenda(politicaResult.Value, agora);
        if (definirResult.IsFailure)
            return Result.Failure<PoliticaAgendaResponse>(definirResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ObterPoliticaAgendaHandler.MapResponse(treinador.PoliticaAgenda));
    }
}
