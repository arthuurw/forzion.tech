using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Planos.AtualizarPlanoPlataforma;

public class AtualizarPlanoPlataformaHandler(
    IPlanoPlataformaRepository planoRepository,
    IUnitOfWork unitOfWork,
    IValidator<AtualizarPlanoPlataformaCommand> validator,
    TimeProvider timeProvider)
{
    public virtual Task<Result<PlanoPlataformaResponse>> HandleAsync(
        AtualizarPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<PlanoPlataformaResponse>> HandleAsyncCore(
        AtualizarPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var plano = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false)
            ?? throw new PlanoPlataformaNaoEncontradoException();

        var atualizarResult = plano.Atualizar(command.Nome, command.Tier, command.MaxAlunos, command.Preco, timeProvider.GetUtcNow().UtcDateTime, command.Descricao);
        if (atualizarResult.IsFailure)
            return Result.Failure<PlanoPlataformaResponse>(atualizarResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(PlanoPlataformaResponseExtensions.ToResponse(plano));
    }
}
