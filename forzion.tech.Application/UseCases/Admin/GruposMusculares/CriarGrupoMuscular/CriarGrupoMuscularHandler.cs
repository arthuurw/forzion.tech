using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;

public class CriarGrupoMuscularHandler(
    IGrupoMuscularRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<CriarGrupoMuscularCommand> validator,
    TimeProvider timeProvider)
{
    public virtual Task<Result<GrupoMuscularResponse>> HandleAsync(CriarGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<GrupoMuscularResponse>> HandleAsyncCore(CriarGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var existente = await repository.ObterPorNomeAsync(command.Nome, cancellationToken);
        if (existente != null)
            return Result.Failure<GrupoMuscularResponse>(Error.Business("Já existe um grupo muscular com este nome."));

        var grupoResult = GrupoMuscular.Criar(command.Nome, timeProvider.GetUtcNow().UtcDateTime);
        if (grupoResult.IsFailure)
            return Result.Failure<GrupoMuscularResponse>(grupoResult.Error!);
        var grupo = grupoResult.Value;

        await repository.AdicionarAsync(grupo, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(new GrupoMuscularResponse(grupo.Id, grupo.Nome, grupo.CreatedAt, grupo.UpdatedAt));
    }
}
