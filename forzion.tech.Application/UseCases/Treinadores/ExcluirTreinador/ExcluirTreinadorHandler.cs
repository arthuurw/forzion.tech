using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.ExcluirTreinador;

public class ExcluirTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    ILogger<ExcluirTreinadorHandler> logger)
{
    public virtual Task<Result> HandleAsync(
        ExcluirTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        ExcluirTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var validacaoResult = treinador.ValidarParaExclusao();
        if (validacaoResult.IsFailure)
            return Result.Failure(validacaoResult.Error!);

        await treinadorRepository.ExcluirComDependenciasAsync(treinador, command.AdminId, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treinador {TreinadorId} excluído permanentemente pelo admin {AdminId}.", treinador.Id, command.AdminId);

        return Result.Success();
    }
}
