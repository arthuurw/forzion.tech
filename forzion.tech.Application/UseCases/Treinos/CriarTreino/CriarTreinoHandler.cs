using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.CriarTreino;

public class CriarTreinoHandler(
    ITreinoRepository treinoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IAlunoRepository alunoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IValidator<CriarTreinoCommand> validator,
    TimeProvider timeProvider,
    ILogger<CriarTreinoHandler> logger)
{
    public virtual Task<Result<TreinoResponse>> HandleAsync(
        CriarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<TreinoResponse>> HandleAsyncCore(
        CriarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        if (command.AlunoId.HasValue)
        {
            _ = await alunoRepository
                .ObterPorIdAsync(command.AlunoId.Value, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new AlunoNaoEncontradoException();

            if (!userContext.IsSystemAdmin)
            {
                var vinculo = await vinculoRepository
                    .ObterAtivoAsync(userContext.PerfilId, command.AlunoId.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (vinculo is null)
                    throw new AcessoNegadoException();
            }
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var treinoResult = Treino.Criar(command.Nome, command.Objetivo, command.TreinadorId, agora, command.Dificuldade, command.DataInicio, command.DataFim);
        if (treinoResult.IsFailure)
            return Result.Failure<TreinoResponse>(treinoResult.Error!);
        var treino = treinoResult.Value;
        await treinoRepository.AdicionarAsync(treino, cancellationToken).ConfigureAwait(false);

        if (command.AlunoId.HasValue)
        {
            var treinoAlunoResult = TreinoAluno.Criar(treino.Id, command.AlunoId.Value, agora);
            if (treinoAlunoResult.IsFailure)
                return Result.Failure<TreinoResponse>(treinoAlunoResult.Error!);
            var treinoAluno = treinoAlunoResult.Value;
            await treinoAlunoRepository.AdicionarAsync(treinoAluno, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treino {TreinoId} criado{Aluno}.",
            treino.Id,
            command.AlunoId.HasValue ? $" para o aluno {command.AlunoId.Value}" : " sem aluno vinculado");

        return Result.Success(TreinoResponseExtensions.ToResponse(treino));
    }
}
