using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.ExcluirTreino;

public class ExcluirTreinoHandler(
    ITreinoRepository treinoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<ExcluirTreinoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;
    private readonly IExecucaoTreinoRepository _execucaoTreinoRepository = execucaoTreinoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserContext _userContext = userContext;
    private readonly ILogger<ExcluirTreinoHandler> _logger = logger;

    public virtual async Task HandleAsync(
        ExcluirTreinoCommand command,
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

        if (executado)
            throw new DomainException("Não é possível excluir uma ficha que já foi executada.");

        await _treinoAlunoRepository
            .RemoverPorTreinoIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false);

        await _treinoRepository.RemoverAsync(treino, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Treino {TreinoId} excluído pelo treinador {TreinadorId}.", command.TreinoId, _userContext.PerfilId);
    }
}
