using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.AtribuirPlano;

public class AtribuirPlanoHandler(
    ITreinadorRepository treinadorRepository,
    IPlanoPlataformaRepository planoRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<AtribuirPlanoHandler> logger)
{
    public virtual Task<Result<TreinadorResponse>> HandleAsync(
        AtribuirPlanoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<TreinadorResponse>> HandleAsyncCore(
        AtribuirPlanoCommand command,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var plano = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false)
            ?? throw new PlanoPlataformaNaoEncontradoException();

        if (plano.Tier == TierPlano.Elite)
            return Result.Failure<TreinadorResponse>(PlanoPlataformaErrors.EliteIndisponivel);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var atribuirResult = treinador.AtribuirPlano(command.PlanoId, agora);
        if (atribuirResult.IsFailure)
            return Result.Failure<TreinadorResponse>(atribuirResult.Error!);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AtribuicaoPlanTreinador,
            command.AdminId,
            treinador.Id,
            nameof(Treinador),
            agora,
            $"Plano {command.PlanoId} atribuído.");
        if (logResult.IsFailure)
            return Result.Failure<TreinadorResponse>(logResult.Error!);
        var log = logResult.Value;

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Plano {PlanoId} atribuído ao treinador {TreinadorId}.", command.PlanoId, treinador.Id);

        return Result.Success(new TreinadorResponse(treinador.Id, treinador.ContaId, treinador.Nome, treinador.Status, treinador.PlanoPlataformaId, treinador.CreatedAt));
    }
}
