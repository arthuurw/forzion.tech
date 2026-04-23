using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.ReprovarTreinador;

public class ReprovarTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    ILogger<ReprovarTreinadorHandler> logger)
{
    public virtual async Task HandleAsync(
        ReprovarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        treinador.Reprovar(command.AdminId);

        var log = LogAprovacao.Registrar(
            TipoAcaoAprovacao.ReprovacaoTreinador,
            command.AdminId,
            treinador.Id,
            nameof(Treinador),
            command.Observacao);

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treinador {TreinadorId} reprovado por {AdminId}.", treinador.Id, command.AdminId);
    }
}
