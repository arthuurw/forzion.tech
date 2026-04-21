using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.AtribuirPlano;

public class AtribuirPlanoHandler(
    ITreinadorRepository treinadorRepository,
    IPlanoTreinadorRepository planoRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    ILogger<AtribuirPlanoHandler> logger)
{
    public virtual async Task<TreinadorResponse> HandleAsync(
        AtribuirPlanoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (treinador.Status == TreinadorStatus.Inativo)
            throw new DomainException("Não é possível atribuir plano a um treinador inativo.");

        _ = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Plano não encontrado.");

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

        return new TreinadorResponse(treinador.Id, treinador.ContaId, treinador.Nome, treinador.Status, treinador.PlanoTreinadorId, treinador.CreatedAt);
    }
}
