using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;

public class AtualizarGrupoMuscularHandler(
    IGrupoMuscularRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<AtualizarGrupoMuscularCommand> validator)
{
    public virtual Task<GrupoMuscularResponse> HandleAsync(AtualizarGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<GrupoMuscularResponse> HandleAsyncCore(AtualizarGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var grupo = await repository.ObterPorIdAsync(command.Id, cancellationToken)
            ?? throw new GrupoMuscularNaoEncontradoException();

        var existente = await repository.ObterPorNomeAsync(command.Nome, cancellationToken);
        if (existente != null && existente.Id != command.Id)
            throw new DomainException("Já existe outro grupo muscular com este nome.");

        var atualizarResult = grupo.Atualizar(command.Nome, timeProvider.GetUtcNow().UtcDateTime);
        if (atualizarResult.IsFailure)
            throw new DomainException(atualizarResult.Error!.Message);

        await unitOfWork.CommitAsync(cancellationToken);

        return new GrupoMuscularResponse(grupo.Id, grupo.Nome, grupo.CreatedAt, grupo.UpdatedAt);
    }
}
