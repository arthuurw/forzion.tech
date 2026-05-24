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

        treinador.AtribuirPlano(command.PlanoId);

        var log = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AtribuicaoPlanTreinador,
            command.AdminId,
            treinador.Id,
            nameof(Treinador),
            $"Plano {command.PlanoId} atribuído.");

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Plano {PlanoId} atribuído ao treinador {TreinadorId}.", command.PlanoId, treinador.Id);

        return new TreinadorResponse(treinador.Id, treinador.ContaId, treinador.Nome, treinador.Status, treinador.PlanoPlataformaId, treinador.CreatedAt);
    }
}
