using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Exercicios.CriarExercicio;

public class CriarExercicioHandler(
    IExercicioRepository exercicioRepository,
    IUnitOfWork unitOfWork,
    IValidator<CriarExercicioCommand> validator,
    ILogger<CriarExercicioHandler> logger)
{
    private readonly IExercicioRepository _exercicioRepository = exercicioRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<CriarExercicioCommand> _validator = validator;
    private readonly ILogger<CriarExercicioHandler> _logger = logger;

    public virtual async Task<ExercicioResponse> HandleAsync(
        CriarExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        if (await _exercicioRepository.NomeJaExisteAsync(command.Nome, command.TreinadorId, cancellationToken: cancellationToken).ConfigureAwait(false))
            throw new Domain.Exceptions.DomainException("Já existe um exercício com este nome nesta biblioteca.");

        var exercicio = Exercicio.Criar(command.Nome, command.GrupoMuscular, command.TreinadorId, command.Descricao);

        await _exercicioRepository.AdicionarAsync(exercicio, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Exercício {ExercicioId} criado.", exercicio.Id);

        return ExercicioResponseExtensions.ToResponse(exercicio);
    }
}
