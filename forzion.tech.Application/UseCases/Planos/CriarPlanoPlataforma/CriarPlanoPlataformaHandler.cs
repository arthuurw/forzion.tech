using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
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
    public virtual Task<Result<PlanoPlataformaResponse>> HandleAsync(
        CriarPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<PlanoPlataformaResponse>> HandleAsyncCore(
        CriarPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var planoResult = PlanoPlataforma.Criar(command.Nome, command.Tier, command.MaxAlunos, command.Preco, timeProvider.GetUtcNow().UtcDateTime, command.Descricao);
        if (planoResult.IsFailure)
            return Result.Failure<PlanoPlataformaResponse>(planoResult.Error!);
        var plano = planoResult.Value;

        await planoRepository.AdicionarAsync(plano, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Plano {PlanoId} '{Nome}' criado por admin {AdminId}.", plano.Id, plano.Nome, userContext.ContaId);

        return Result.Success(PlanoPlataformaResponseExtensions.ToResponse(plano));
    }
}
