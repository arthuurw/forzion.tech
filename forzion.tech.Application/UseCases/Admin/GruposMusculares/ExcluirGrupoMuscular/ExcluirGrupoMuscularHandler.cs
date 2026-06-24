using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;

public class ExcluirGrupoMuscularHandler(
    IGrupoMuscularRepository repository,
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork,
    ILogAprovacaoRepository logRepository,
    ILogger<ExcluirGrupoMuscularHandler> logger,
    TimeProvider timeProvider,
    IUserContext userContext)
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
            return Result.Failure(Error.Conflict("grupo_muscular.em_uso", "Não é possível excluir um grupo muscular em uso por exercícios."));

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var logResult = await logRepository.RegistrarAsync(
            TipoAcaoAprovacao.ExclusaoGrupoMuscular,
            userContext.PerfilId,
            grupo.Id,
            nameof(GrupoMuscular),
            agora,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);

        repository.Excluir(grupo);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("GrupoMuscular {GrupoId} excluído por {AtorId}.", grupo.Id, userContext.PerfilId);

        return Result.Success();
    }
}
