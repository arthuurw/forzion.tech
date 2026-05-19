using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Planos.AtualizarPlanoTreinador;

public class AtualizarPlanoTreinadorHandler(
    IPlanoTreinadorRepository planoRepository,
    IUnitOfWork unitOfWork,
    IValidator<AtualizarPlanoTreinadorCommand> validator)
{
    public virtual Task<PlanoTreinadorResponse> HandleAsync(
        AtualizarPlanoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<PlanoTreinadorResponse> HandleAsyncCore(
        AtualizarPlanoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var plano = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false)
            ?? throw new PlanoTreinadorNaoEncontradoException();

        plano.Atualizar(command.Nome, command.Tier, command.MaxAlunos, command.Preco, command.Descricao);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PlanoTreinadorResponseExtensions.ToResponse(plano);
    }
}
