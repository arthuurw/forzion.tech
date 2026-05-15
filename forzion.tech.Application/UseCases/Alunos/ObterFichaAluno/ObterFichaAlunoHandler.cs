using forzion.tech.Application.Interfaces;
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

public class ObterFichaAlunoHandler(ITreinoAlunoRepository treinoAlunoRepository, IUserContext userContext)
{
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;
    private readonly IUserContext _userContext = userContext;

    public virtual async Task<FichaAlunoDetalheResponse> HandleAsync(
        Guid treinoAlunoId,
        Guid alunoId,
        CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsSystemAdmin && _userContext.PerfilId != alunoId)
            throw new AcessoNegadoException();

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
            [.. detalhe.Treino.Exercicios.OrderBy(te => te.Ordem).Select(te => new TreinoExercicioResponse(
                te.Id,
                te.ExercicioId,
                te.Exercicio?.Nome ?? string.Empty,
                te.Series.OrderBy(s => s.Ordem).Select(s => new SerieConfigResponse(
                    s.Id, s.Quantidade, s.RepeticoesMin, s.RepeticoesMax,
                    s.Descricao, s.Carga, s.Descanso, s.Ordem)).ToList(),
                te.Ordem,
                te.Observacao))]);
    }
}
