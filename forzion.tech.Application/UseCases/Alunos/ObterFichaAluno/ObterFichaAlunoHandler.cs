using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Alunos.ObterFichaAluno;

public record FichaAlunoDetalheResponse(
    Guid TreinoAlunoId,
    Guid TreinoId,
    string NomeTreino,
    ObjetivoTreino Objetivo,
    string Status,
    IReadOnlyList<TreinoExercicioResponse> Exercicios);

public class ObterFichaAlunoHandler(ITreinoAlunoRepository treinoAlunoRepository)
{
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;

    public virtual async Task<FichaAlunoDetalheResponse> HandleAsync(
        Guid treinoAlunoId,
        Guid alunoId,
        CancellationToken cancellationToken = default)
    {
        var detalhe = await _treinoAlunoRepository
            .ObterDetalheAsync(treinoAlunoId, alunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        return new FichaAlunoDetalheResponse(
            detalhe.TreinoAluno.Id,
            detalhe.Treino.Id,
            detalhe.Treino.Nome,
            detalhe.Treino.Objetivo,
            detalhe.TreinoAluno.Status.ToString(),
            detalhe.Treino.Exercicios.Select(te => new TreinoExercicioResponse(
                te.Id,
                te.ExercicioId,
                te.Exercicio?.Nome ?? string.Empty,
                te.Series,
                te.Repeticoes,
                te.Carga,
                te.Descanso,
                te.Ordem)).ToList());
    }
}
