using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Exercicios.CriarExercicio;

public class CriarExercicioHandler(
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork,
    ILogger<CriarExercicioHandler> logger)
{
    private readonly IExercicioRepository _exercicioRepository = exercicioRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CriarExercicioHandler> _logger = logger;

    public virtual async Task<ExercicioResponse> HandleAsync(
        CriarExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var exercicio = Exercicio.Criar(command.Nome, command.GrupoMuscular, command.TenantId, command.Descricao);

        await _exercicioRepository.AdicionarAsync(exercicio, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Exercício {ExercicioId} criado no tenant {TenantId}.", exercicio.Id, command.TenantId);

        return ExercicioResponseExtensions.ToResponse(exercicio);
    }
}
