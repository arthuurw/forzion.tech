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
    private readonly IGrupoMuscularRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<CriarGrupoMuscularCommand> _validator = validator;

    public virtual async Task<GrupoMuscularResponse> HandleAsync(CriarGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var existente = await _repository.ObterPorNomeAsync(command.Nome, cancellationToken);
        if (existente != null)
            throw new DomainException("Já existe um grupo muscular com este nome.");

        var grupo = GrupoMuscular.Criar(command.Nome);
        
        await _repository.AdicionarAsync(grupo, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return new GrupoMuscularResponse(grupo.Id, grupo.Nome, grupo.CreatedAt, grupo.UpdatedAt);
    }
}
