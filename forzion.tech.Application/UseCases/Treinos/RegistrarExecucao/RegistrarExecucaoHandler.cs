using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;

public class RegistrarExecucaoHandler(
    ITreinoRepository treinoRepository,
    IAlunoRepository alunoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<RegistrarExecucaoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;
    private readonly IVinculoTreinadorAlunoRepository _vinculoRepository = vinculoRepository;
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

        var treino = await _treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        var treinoAluno = await _treinoAlunoRepository
            .ObterAsync(command.TreinoId, command.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (treinoAluno is null || treinoAluno.Status != TreinoAlunoStatus.Ativo)
            throw new AcessoNegadoException();

        var vinculo = await _vinculoRepository.ObterAtivoAsync(treino.TreinadorId, command.AlunoId, cancellationToken).ConfigureAwait(false);
        if (vinculo is null)
            throw new AcessoNegadoException();

        var aluno = await _alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        if (aluno.Status == Domain.Enums.AlunoStatus.Inativo)
            throw new AlunoInativoException();

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
