using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.EditarExercicioTreino;

public class EditarExercicioTreinoHandler(
    ITreinoRepository treinoRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<EditarExercicioTreinoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IExecucaoTreinoRepository _execucaoTreinoRepository = execucaoTreinoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserContext _userContext = userContext;
    private readonly ILogger<EditarExercicioTreinoHandler> _logger = logger;

    public virtual async Task<Result<TreinoResponse>> HandleAsync(
        EditarExercicioTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treino = await _treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (!_userContext.IsSystemAdmin && treino.TreinadorId != _userContext.PerfilId)
            throw new AcessoNegadoException();

        var executado = await _execucaoTreinoRepository
            .ExisteParaTreinoAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false);

        treino.ValidarMutabilidade(executado);

        var exercicio = treino.Exercicios.FirstOrDefault(e => e.Id == command.TreinoExercicioId)
            ?? throw new TreinoNaoEncontradoException();

        try
        {
            exercicio.AtualizarSeries(command.Series
                .Select(s => (s.Quantidade, s.RepeticoesMin, s.RepeticoesMax, s.Descricao, s.Carga, s.Descanso))
                .ToList());
        }
        catch (DomainException ex)
        {
            return Result.Failure<TreinoResponse>(Error.Business(ex.Message));
        }

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Exercício {TreinoExercicioId} editado no treino {TreinoId}.",
            command.TreinoExercicioId, command.TreinoId);

        return Result.Success(TreinoResponseExtensions.ToResponse(treino));
    }
}
