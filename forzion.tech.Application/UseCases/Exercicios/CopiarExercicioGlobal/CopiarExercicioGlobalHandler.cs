using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Exercicios.CopiarExercicioGlobal;

public class CopiarExercicioGlobalHandler(
    IExercicioRepository exercicioRepository,
    IGrupoMuscularRepository grupoMuscularRepository,
    IUnitOfWork unitOfWork,
    ILogger<CopiarExercicioGlobalHandler> logger)
{
    public virtual Task<ExercicioResponse> HandleAsync(
        CopiarExercicioGlobalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<ExercicioResponse> HandleAsyncCore(
        CopiarExercicioGlobalCommand command,
        CancellationToken cancellationToken = default)
    {
        var original = await exercicioRepository.ObterPorIdAsync(command.ExercicioId, cancellationToken).ConfigureAwait(false)
            ?? throw new ExercicioNaoEncontradoException();

        if (!original.IsGlobal)
            throw new AcessoNegadoException();

        var copia = Exercicio.Criar(original.Nome, original.GrupoMuscularId, command.TreinadorId, original.Descricao);

        await exercicioRepository.AdicionarAsync(copia, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Exercício global {OriginalId} copiado para treinador {TreinadorId} como {CopiaId}.",
            command.ExercicioId, command.TreinadorId, copia.Id);

        var grupoMuscular = await grupoMuscularRepository.ObterPorIdAsync(copia.GrupoMuscularId, cancellationToken).ConfigureAwait(false)
            ?? throw new GrupoMuscularNaoEncontradoException();

        return ExercicioResponseExtensions.ToResponse(copia, grupoMuscular.Nome);
    }
}
