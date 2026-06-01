using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;

public class AprovarTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<AprovarTreinadorHandler> logger)
{
    public virtual Task<Result<TreinadorResponse>> HandleAsync(
        AprovarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<TreinadorResponse>> HandleAsyncCore(
        AprovarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var aprovarResult = treinador.Aprovar(command.AdminId, agora);
        if (aprovarResult.IsFailure)
            return Result.Failure<TreinadorResponse>(aprovarResult.Error!);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AprovacaoTreinador,
            command.AdminId,
            treinador.Id,
            nameof(Treinador),
            agora,
            command.Observacao);
        if (logResult.IsFailure)
            return Result.Failure<TreinadorResponse>(logResult.Error!);
        var log = logResult.Value;

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treinador {TreinadorId} aprovado por {AdminId}.", treinador.Id, command.AdminId);

        return Result.Success(new TreinadorResponse(treinador.Id, treinador.ContaId, treinador.Nome, treinador.Status, treinador.PlanoPlataformaId, treinador.CreatedAt));
    }
}
