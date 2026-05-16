using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
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
        var treinadorId = userContext.PerfilId;

        var treino = await treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (treino.TreinadorId != treinadorId)
            throw new AcessoNegadoException();

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
            return Result.Failure(Error.Business($"Esta ficha já está vinculada ao aluno {nomeExistente}."));
        }

        var treinoAluno = TreinoAluno.Criar(command.TreinoId, command.AlunoId);

        await treinoAlunoRepository.AdicionarAsync(treinoAluno, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Ficha {TreinoId} vinculada ao aluno {AlunoId} pelo treinador {TreinadorId}.",
            command.TreinoId, command.AlunoId, treinadorId);

        return Result.Success();
    }
}
