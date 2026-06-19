using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
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
    TimeProvider timeProvider,
    IDatabaseErrorInspector databaseErrorInspector,
    ILogger<RegistrarExecucaoHandler> logger)
{
    public virtual Task<Result<RegistrarExecucaoResponse>> HandleAsync(
        RegistrarExecucaoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<RegistrarExecucaoResponse>> HandleAsyncCore(
        RegistrarExecucaoCommand command,
        CancellationToken cancellationToken = default)
    {
        if (userContext.PerfilId != command.AlunoId)
            throw new AcessoNegadoException();

        var treino = await treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        var treinoAluno = await treinoAlunoRepository
            .ObterAsync(command.TreinoId, command.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (treinoAluno is null || treinoAluno.Status != TreinoAlunoStatus.Ativo)
            throw new AcessoNegadoException();

        var vinculo = await vinculoRepository.ObterAtivoAsync(treino.TreinadorId, command.AlunoId, cancellationToken).ConfigureAwait(false);
        if (vinculo is null)
            throw new AcessoNegadoException();

        var aluno = await alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        if (aluno.Status == Domain.Enums.AlunoStatus.Inativo)
            throw new AlunoInativoException();

        if (command.IdempotencyKey is not null)
        {
            var existente = await execucaoTreinoRepository
                .ObterPorIdempotencyKeyAsync(command.AlunoId, command.IdempotencyKey, cancellationToken)
                .ConfigureAwait(false);
            if (existente is not null)
                return Result.Success(MapToResponse(existente));
        }

        var execucaoResult = ExecucaoTreino.Criar(
            command.TreinoId, command.AlunoId, command.DataExecucao, timeProvider.GetUtcNow().UtcDateTime, command.Observacao, command.IdempotencyKey);
        if (execucaoResult.IsFailure)
            return Result.Failure<RegistrarExecucaoResponse>(execucaoResult.Error!);
        var execucao = execucaoResult.Value;

        foreach (var item in command.Exercicios)
        {
            var exercicioResult = execucao.AdicionarExercicio(
                item.TreinoExercicioId,
                item.SeriesExecutadas,
                item.RepeticoesExecutadas,
                item.CargaExecutada,
                item.Observacao);
            if (exercicioResult.IsFailure)
                return Result.Failure<RegistrarExecucaoResponse>(exercicioResult.Error!);
        }

        await execucaoTreinoRepository.AdicionarAsync(execucao, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        // Corrida: dois requests com a mesma key passam o lookup acima e colidem no unique index.
        // A violação significa que o concorrente já gravou → re-consulta e retorna idempotente.
        catch (Exception ex) when (command.IdempotencyKey is not null && databaseErrorInspector.EhViolacaoDeUnicidade(ex))
        {
            var existente = await execucaoTreinoRepository
                .ObterPorIdempotencyKeyAsync(command.AlunoId, command.IdempotencyKey, cancellationToken)
                .ConfigureAwait(false);
            if (existente is null)
                throw;
            return Result.Success(MapToResponse(existente));
        }

        logger.LogInformation("Execução {ExecucaoId} registrada para o treino {TreinoId} pelo aluno {AlunoId}.",
            execucao.Id, command.TreinoId, command.AlunoId);

        return Result.Success(MapToResponse(execucao));
    }

    private static RegistrarExecucaoResponse MapToResponse(ExecucaoTreino execucao) =>
        new(execucao.Id,
            execucao.TreinoId,
            execucao.AlunoId,
            execucao.DataExecucao,
            execucao.Observacao,
            execucao.CreatedAt);
}
