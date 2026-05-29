using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;

public class ExcluirGrupoMuscularHandler(
    IGrupoMuscularRepository repository,
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork)
{
    public virtual Task<Result> HandleAsync(ExcluirGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(ExcluirGrupoMuscularCommand command, CancellationToken cancellationToken = default)
    {
        var grupo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (grupo is null)
            return Result.Failure(Error.NotFound("grupo_muscular_nao_encontrado", "Grupo muscular não encontrado."));

        if (await exercicioRepository.ExisteComGrupoMuscularAsync(command.Id, cancellationToken).ConfigureAwait(false))
            return Result.Failure(Error.Business("grupo_muscular_em_uso", "Não é possível excluir um grupo muscular em uso por exercícios."));

        repository.Excluir(grupo);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
