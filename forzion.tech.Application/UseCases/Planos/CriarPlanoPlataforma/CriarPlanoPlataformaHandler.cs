using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Planos.CriarPlanoPlataforma;

public class CriarPlanoPlataformaHandler(
    IPlanoPlataformaRepository planoRepository,
    IUnitOfWork unitOfWork,
    IValidator<CriarPlanoPlataformaCommand> validator,
    IUserContext userContext,
    TimeProvider timeProvider,
    ILogger<CriarPlanoPlataformaHandler> logger)
{
    public virtual Task<PlanoPlataformaResponse> HandleAsync(
        CriarPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<PlanoPlataformaResponse> HandleAsyncCore(
        CriarPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var plano = PlanoPlataforma.Criar(command.Nome, command.Tier, command.MaxAlunos, command.Preco, timeProvider.GetUtcNow().UtcDateTime, command.Descricao);

        await planoRepository.AdicionarAsync(plano, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Plano {PlanoId} '{Nome}' criado por admin {AdminId}.", plano.Id, plano.Nome, userContext.ContaId);

        return PlanoPlataformaResponseExtensions.ToResponse(plano);
    }
}
