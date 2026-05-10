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
    private readonly IValidator<AtualizarPlanoTreinadorCommand> _validator = validator;

    public virtual async Task<PlanoTreinadorResponse> HandleAsync(
        AtualizarPlanoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var plano = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false)
            ?? throw new PlanoTreinadorNaoEncontradoException();

        plano.Atualizar(command.Nome, command.MaxAlunos, command.Preco);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PlanoTreinadorResponseExtensions.ToResponse(plano);
    }
}
