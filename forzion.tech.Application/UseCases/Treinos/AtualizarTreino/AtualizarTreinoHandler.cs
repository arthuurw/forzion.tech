using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.AtualizarTreino;

public class AtualizarTreinoHandler(
    ITreinoRepository treinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<AtualizarTreinoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserContext _userContext = userContext;
    private readonly ILogger<AtualizarTreinoHandler> _logger = logger;

    public virtual async Task<Result<TreinoResponse>> HandleAsync(
        AtualizarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treino = await _treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (!_userContext.IsSystemAdmin && treino.TreinadorId != _userContext.PerfilId)
            throw new AcessoNegadoException();

        try
        {
            treino.Atualizar(command.Nome, command.Objetivo, command.Dificuldade, command.DataInicio, command.DataFim, command.LimparDataInicio, command.LimparDataFim);
        }
        catch (DomainException ex)
        {
            return Result.Failure<TreinoResponse>(Error.Business(ex.Message));
        }

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Treino {TreinoId} atualizado.", command.TreinoId);

        return Result.Success(TreinoResponseExtensions.ToResponse(treino));
    }
}
