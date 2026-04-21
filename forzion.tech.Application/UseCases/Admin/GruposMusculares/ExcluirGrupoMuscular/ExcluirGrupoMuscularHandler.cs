using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;

public class ExcluirGrupoMuscularHandler(
    IGrupoMuscularRepository repository,
    IUnitOfWork unitOfWork)
{
    private readonly IGrupoMuscularRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task HandleAsync(ExcluirGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        var grupo = await _repository.ObterPorIdAsync(command.Id, cancellationToken)
            ?? throw new DomainException("Grupo muscular não encontrado.");

        // TODO: Validar se existem exercícios vinculados quando a migração Enum -> Entity ocorrer

        _repository.Excluir(grupo);
        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
