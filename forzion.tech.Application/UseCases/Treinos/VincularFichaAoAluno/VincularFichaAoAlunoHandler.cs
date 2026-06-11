using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;

public class VincularFichaAoAlunoHandler(
    ITreinoRepository treinoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    TimeProvider timeProvider,
    ILogger<VincularFichaAoAlunoHandler> logger)
{
    public virtual Task<Result> HandleAsync(
        VincularFichaAoAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        VincularFichaAoAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        var treino = await treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        // SystemAdmin pode agir em nome de qualquer treinador (consistência com os
        // demais handlers do grupo /treinos); treinador só na própria ficha.
        if (!userContext.IsSystemAdmin && treino.TreinadorId != userContext.PerfilId)
            throw new AcessoNegadoException();

        // Vínculo/ficha resolvidos pelo dono do treino (admin agindo em nome dele).
        var treinadorId = treino.TreinadorId;

        _ = await vinculoRepository
            .ObterAtivoAsync(treinadorId, command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new VinculoNaoEncontradoException();

        var alunosVinculados = await treinoAlunoRepository
            .ListarAtivosPorTreinoIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false);

        if (alunosVinculados.Count > 0)
        {
            var nomeExistente = alunosVinculados[0].NomeAluno;
            return Result.Failure(Error.Business("ficha.ja_vinculada", $"Esta ficha já está vinculada ao aluno {nomeExistente}."));
        }

        var treinoAlunoResult = TreinoAluno.Criar(command.TreinoId, command.AlunoId, timeProvider.GetUtcNow().UtcDateTime);
        if (treinoAlunoResult.IsFailure)
            return Result.Failure(treinoAlunoResult.Error!);
        var treinoAluno = treinoAlunoResult.Value;

        await treinoAlunoRepository.AdicionarAsync(treinoAluno, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Ficha {TreinoId} vinculada ao aluno {AlunoId} pelo treinador {TreinadorId}.",
            command.TreinoId, command.AlunoId, treinadorId);

        return Result.Success();
    }
}
