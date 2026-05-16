using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.AtualizarObservacaoExercicio;

public class AtualizarObservacaoExercicioHandler(
    ITreinoRepository treinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<AtualizarObservacaoExercicioHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserContext _userContext = userContext;
    private readonly ILogger<AtualizarObservacaoExercicioHandler> _logger = logger;

    public virtual async Task<TreinoResponse> HandleAsync(
        AtualizarObservacaoExercicioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treino = await _treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (!_userContext.IsSystemAdmin && treino.TreinadorId != _userContext.PerfilId)
            throw new AcessoNegadoException();

        var exercicio = treino.Exercicios.FirstOrDefault(e => e.Id == command.TreinoExercicioId)
            ?? throw new TreinoNaoEncontradoException();

        exercicio.AtualizarObservacao(command.Observacao);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Observação atualizada no exercício {ExercicioId} do treino {TreinoId}.",
            command.TreinoExercicioId, command.TreinoId);

        return TreinoResponseExtensions.ToResponse(treino);
    }
}
