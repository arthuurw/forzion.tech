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
    ILogger<RegistrarExecucaoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IExecucaoTreinoRepository _execucaoTreinoRepository = execucaoTreinoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<RegistrarExecucaoHandler> _logger = logger;

    public virtual async Task<RegistrarExecucaoResponse> HandleAsync(
        RegistrarExecucaoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treino = await _treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (treino.TenantId != command.TenantId)
            throw new AcessoNegadoException();

        var aluno = await _alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        if (aluno.TenantId != command.TenantId)
            throw new AcessoNegadoException();

        var execucao = ExecucaoTreino.Criar(
            command.TreinoId, command.AlunoId, command.TenantId,
            command.DataExecucao, command.Observacao);

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
            execucao.TenantId,
            execucao.DataExecucao,
            execucao.Observacao,
            execucao.CreatedAt);
    }
}
