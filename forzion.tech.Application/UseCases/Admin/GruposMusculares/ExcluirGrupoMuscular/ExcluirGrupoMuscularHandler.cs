using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;

public class ExcluirGrupoMuscularHandler(
    IGrupoMuscularRepository repository,
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork)
{
    public virtual Task HandleAsync(ExcluirGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task HandleAsyncCore(ExcluirGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        var grupo = await repository.ObterPorIdAsync(command.Id, cancellationToken)
            ?? throw new GrupoMuscularNaoEncontradoException();

        if (await exercicioRepository.ExisteComGrupoMuscularAsync(command.Id, cancellationToken).ConfigureAwait(false))
            throw new DomainException("Não é possível excluir um grupo muscular em uso por exercícios.");

        repository.Excluir(grupo);
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
