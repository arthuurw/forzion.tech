using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.ReprovarTreinador;

public class ReprovarTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ReprovarTreinadorHandler> logger)
{
    public virtual Task<Result> HandleAsync(
        ReprovarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        ReprovarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var reprovarResult = treinador.Reprovar(command.AdminId, agora);
        if (reprovarResult.IsFailure)
            return Result.Failure(reprovarResult.Error!);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.ReprovacaoTreinador,
            command.AdminId,
            treinador.Id,
            nameof(Treinador),
            agora,
            command.Observacao);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);
        var log = logResult.Value;

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treinador {TreinadorId} reprovado por {AdminId}.", treinador.Id, command.AdminId);

        return Result.Success();
    }
}
