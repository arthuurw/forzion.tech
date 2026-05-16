using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;

public class CriarGrupoMuscularHandler(
    IGrupoMuscularRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<CriarGrupoMuscularCommand> validator)
{
    public virtual Task<GrupoMuscularResponse> HandleAsync(CriarGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<GrupoMuscularResponse> HandleAsyncCore(CriarGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var existente = await repository.ObterPorNomeAsync(command.Nome, cancellationToken);
        if (existente != null)
            throw new DomainException("Já existe um grupo muscular com este nome.");

        var grupo = GrupoMuscular.Criar(command.Nome);

        await repository.AdicionarAsync(grupo, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return new GrupoMuscularResponse(grupo.Id, grupo.Nome, grupo.CreatedAt, grupo.UpdatedAt);
    }
}
