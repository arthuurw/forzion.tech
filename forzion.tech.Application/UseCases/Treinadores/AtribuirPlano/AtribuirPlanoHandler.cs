using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
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
    public virtual Task<TreinadorResponse> HandleAsync(
        AtribuirPlanoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<TreinadorResponse> HandleAsyncCore(
        AtribuirPlanoCommand command,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        _ = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false)
            ?? throw new PlanoPlataformaNaoEncontradoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var atribuirResult = treinador.AtribuirPlano(command.PlanoId, agora);
        if (atribuirResult.IsFailure)
            throw new DomainException(atribuirResult.Error!.Message);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AtribuicaoPlanTreinador,
            command.AdminId,
            treinador.Id,
            nameof(Treinador),
            agora,
            $"Plano {command.PlanoId} atribuído.");
        if (logResult.IsFailure)
            throw new DomainException(logResult.Error!.Message);
        var log = logResult.Value;

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Plano {PlanoId} atribuído ao treinador {TreinadorId}.", command.PlanoId, treinador.Id);

        return new TreinadorResponse(treinador.Id, treinador.ContaId, treinador.Nome, treinador.Status, treinador.PlanoPlataformaId, treinador.CreatedAt);
    }
}
