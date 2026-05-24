using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
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
    public virtual Task<TreinoResponse> HandleAsync(
        CriarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<TreinoResponse> HandleAsyncCore(
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
        var treino = Treino.Criar(command.Nome, command.Objetivo, command.TreinadorId, agora, command.Dificuldade, command.DataInicio, command.DataFim);
        await treinoRepository.AdicionarAsync(treino, cancellationToken).ConfigureAwait(false);

        if (command.AlunoId.HasValue)
        {
            var treinoAluno = TreinoAluno.Criar(treino.Id, command.AlunoId.Value, agora);
            await treinoAlunoRepository.AdicionarAsync(treinoAluno, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treino {TreinoId} criado{Aluno}.",
            treino.Id,
            command.AlunoId.HasValue ? $" para o aluno {command.AlunoId.Value}" : " sem aluno vinculado");

        return TreinoResponseExtensions.ToResponse(treino);
    }
}
