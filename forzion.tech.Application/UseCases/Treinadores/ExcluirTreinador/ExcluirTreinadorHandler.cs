using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.ExcluirTreinador;

public class ExcluirTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    ILogger<ExcluirTreinadorHandler> logger)
{
    public virtual Task HandleAsync(
        ExcluirTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task HandleAsyncCore(
        ExcluirTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        treinador.ValidarParaExclusao();

        await treinadorRepository.ExcluirComDependenciasAsync(treinador, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treinador {TreinadorId} excluído permanentemente pelo admin {AdminId}.", treinador.Id, command.AdminId);
    }
}
