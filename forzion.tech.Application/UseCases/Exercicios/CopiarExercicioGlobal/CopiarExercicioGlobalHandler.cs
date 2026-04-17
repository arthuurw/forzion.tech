using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Exercicios.CopiarExercicioGlobal;

public class CopiarExercicioGlobalHandler(
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork,
    ILogger<CopiarExercicioGlobalHandler> logger)
{
    private readonly IExercicioRepository _exercicioRepository = exercicioRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CopiarExercicioGlobalHandler> _logger = logger;

    public virtual async Task<ExercicioResponse> HandleAsync(
        CopiarExercicioGlobalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var original = await _exercicioRepository.ObterPorIdAsync(command.ExercicioId, cancellationToken).ConfigureAwait(false)
            ?? throw new ExercicioNaoEncontradoException();

        if (!original.IsGlobal)
            throw new AcessoNegadoException();

        var copia = Exercicio.Criar(original.Nome, original.GrupoMuscular, command.TreinadorId, original.Descricao);

        await _exercicioRepository.AdicionarAsync(copia, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Exercício global {OriginalId} copiado para treinador {TreinadorId} como {CopiaId}.",
            command.ExercicioId, command.TreinadorId, copia.Id);

        return ExercicioResponseExtensions.ToResponse(copia);
    }
}
