using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;

public class RegistrarExecucaoHandler(
    ITreinoRepository treinoRepository,
    IAlunoRepository alunoRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<RegistrarExecucaoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IExecucaoTreinoRepository _execucaoTreinoRepository = execucaoTreinoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserContext _userContext = userContext;
    private readonly ILogger<RegistrarExecucaoHandler> _logger = logger;

    public virtual async Task<RegistrarExecucaoResponse> HandleAsync(
        RegistrarExecucaoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_userContext.PerfilId != command.AlunoId)
            throw new AcessoNegadoException();

        _ = await _treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        _ = await _alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        var execucao = ExecucaoTreino.Criar(
            command.TreinoId, command.AlunoId, command.DataExecucao, command.Observacao);

        foreach (var item in command.Exercicios)
            execucao.AdicionarExercicio(
                item.TreinoExercicioId,
                item.SeriesExecutadas,
                item.RepeticoesExecutadas,
                item.CargaExecutada,
                item.Observacao);

        await _execucaoTreinoRepository.AdicionarAsync(execucao, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Execução {ExecucaoId} registrada para o treino {TreinoId} pelo aluno {AlunoId}.",
            execucao.Id, command.TreinoId, command.AlunoId);

        return new RegistrarExecucaoResponse(
            execucao.Id,
            execucao.TreinoId,
            execucao.AlunoId,
            execucao.DataExecucao,
            execucao.Observacao,
            execucao.CreatedAt);
    }
}
