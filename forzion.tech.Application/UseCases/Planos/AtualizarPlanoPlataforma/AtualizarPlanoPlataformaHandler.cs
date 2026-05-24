using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Planos.AtualizarPlanoPlataforma;

public class AtualizarPlanoPlataformaHandler(
    IPlanoPlataformaRepository planoRepository,
    IUnitOfWork unitOfWork,
    IValidator<AtualizarPlanoPlataformaCommand> validator)
{
    public virtual Task<PlanoPlataformaResponse> HandleAsync(
        AtualizarPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<PlanoPlataformaResponse> HandleAsyncCore(
        AtualizarPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var plano = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false)
            ?? throw new PlanoPlataformaNaoEncontradoException();

        plano.Atualizar(command.Nome, command.Tier, command.MaxAlunos, command.Preco, command.Descricao);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PlanoPlataformaResponseExtensions.ToResponse(plano);
    }
}
