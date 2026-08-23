using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Treinadores.Agenda;

public class CriarBloqueioAgendaHandler(
    ITreinadorRepository treinadorRepository,
    IBloqueioAgendaRepository bloqueioAgendaRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result<BloqueioAgendaResponse>> HandleAsync(
        CriarBloqueioAgendaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<BloqueioAgendaResponse>> HandleAsyncCore(
        CriarBloqueioAgendaCommand command,
        CancellationToken cancellationToken)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
            return Result.Failure<BloqueioAgendaResponse>(TreinadorErrors.NaoEncontrado);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var bloqueioResult = command.Tipo switch
        {
            TipoBloqueio.Pontual => BloqueioAgenda.CriarPontual(
                command.TreinadorId, command.InicioUtc ?? DateTime.MinValue, command.FimUtc ?? DateTime.MinValue, command.Motivo, agora),
            TipoBloqueio.RecorrenteSemanal => BloqueioAgenda.CriarRecorrente(
                command.TreinadorId, command.DiaSemana ?? -1, command.HoraInicio ?? TimeOnly.MinValue, command.HoraFim ?? TimeOnly.MinValue, command.Motivo, agora),
            _ => Result.Failure<BloqueioAgenda>(BloqueioAgendaErrors.TipoInvalido),
        };

        if (bloqueioResult.IsFailure)
            return Result.Failure<BloqueioAgendaResponse>(bloqueioResult.Error!);

        await bloqueioAgendaRepository.AdicionarAsync(bloqueioResult.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(MapResponse(bloqueioResult.Value));
    }

    internal static BloqueioAgendaResponse MapResponse(BloqueioAgenda bloqueio) => new(
        bloqueio.Id,
        bloqueio.Tipo,
        bloqueio.InicioUtc,
        bloqueio.FimUtc,
        bloqueio.DiaSemana,
        bloqueio.HoraInicio,
        bloqueio.HoraFim,
        bloqueio.Motivo,
        bloqueio.CreatedAt);
}
