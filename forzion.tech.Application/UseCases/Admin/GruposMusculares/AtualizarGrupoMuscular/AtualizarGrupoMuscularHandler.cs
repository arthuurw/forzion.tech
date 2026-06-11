using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;

public class AtualizarGrupoMuscularHandler(
    IGrupoMuscularRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<AtualizarGrupoMuscularCommand> validator)
{
    public virtual Task<Result<GrupoMuscularResponse>> HandleAsync(AtualizarGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<GrupoMuscularResponse>> HandleAsyncCore(AtualizarGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var grupo = await repository.ObterPorIdAsync(command.Id, cancellationToken)
            ?? throw new GrupoMuscularNaoEncontradoException();

        var existente = await repository.ObterPorNomeAsync(command.Nome, cancellationToken);
        if (existente != null && existente.Id != command.Id)
            return Result.Failure<GrupoMuscularResponse>(Error.Business("grupo_muscular.nome_duplicado", "Já existe outro grupo muscular com este nome."));

        var atualizarResult = grupo.Atualizar(command.Nome, timeProvider.GetUtcNow().UtcDateTime);
        if (atualizarResult.IsFailure)
            return Result.Failure<GrupoMuscularResponse>(atualizarResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(new GrupoMuscularResponse(grupo.Id, grupo.Nome, grupo.CreatedAt, grupo.UpdatedAt));
    }
}
