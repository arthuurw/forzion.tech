using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.ExcluirTreinador;

public class ExcluirTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    ILogger<ExcluirTreinadorHandler> logger)
{
    public virtual async Task HandleAsync(
        ExcluirTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (treinador.Status != TreinadorStatus.Inativo)
            throw new DomainException("Apenas treinadores inativos podem ser excluídos permanentemente.");

        await treinadorRepository.ExcluirComDependenciasAsync(treinador, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treinador {TreinadorId} excluído permanentemente pelo admin {AdminId}.", treinador.Id, command.AdminId);
    }
}
