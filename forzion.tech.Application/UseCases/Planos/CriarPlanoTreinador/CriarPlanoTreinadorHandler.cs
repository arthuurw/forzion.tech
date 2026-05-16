using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Planos.CriarPlanoTreinador;

public class CriarPlanoTreinadorHandler(
    IPlanoTreinadorRepository planoRepository,
    IUnitOfWork unitOfWork,
    IValidator<CriarPlanoTreinadorCommand> validator,
    IUserContext userContext,
    ILogger<CriarPlanoTreinadorHandler> logger)
{
    public virtual Task<PlanoTreinadorResponse> HandleAsync(
        CriarPlanoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<PlanoTreinadorResponse> HandleAsyncCore(
        CriarPlanoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var plano = PlanoTreinador.Criar(command.Nome, command.MaxAlunos, command.Preco);

        await planoRepository.AdicionarAsync(plano, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Plano {PlanoId} '{Nome}' criado por admin {AdminId}.", plano.Id, plano.Nome, userContext.ContaId);

        return PlanoTreinadorResponseExtensions.ToResponse(plano);
    }
}
