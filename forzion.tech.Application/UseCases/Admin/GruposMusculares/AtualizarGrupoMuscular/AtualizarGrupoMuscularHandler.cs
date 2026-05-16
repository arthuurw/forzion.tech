using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;

public class AtualizarGrupoMuscularHandler(
    IGrupoMuscularRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<AtualizarGrupoMuscularCommand> validator)
{
    private readonly IGrupoMuscularRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<AtualizarGrupoMuscularCommand> _validator = validator;

    public virtual async Task<GrupoMuscularResponse> HandleAsync(AtualizarGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var grupo = await _repository.ObterPorIdAsync(command.Id, cancellationToken)
            ?? throw new GrupoMuscularNaoEncontradoException();

        var existente = await _repository.ObterPorNomeAsync(command.Nome, cancellationToken);
        if (existente != null && existente.Id != command.Id)
            throw new DomainException("Já existe outro grupo muscular com este nome.");

        grupo.Atualizar(command.Nome);
        
        await _unitOfWork.CommitAsync(cancellationToken);

        return new GrupoMuscularResponse(grupo.Id, grupo.Nome, grupo.CreatedAt, grupo.UpdatedAt);
    }
}
