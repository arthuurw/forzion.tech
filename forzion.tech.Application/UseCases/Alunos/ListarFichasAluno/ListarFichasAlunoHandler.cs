using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;

public record FichaAlunoResponse(
    Guid TreinoAlunoId,
    Guid TreinoId,
    string NomeTreino,
    ObjetivoTreino Objetivo,
    TreinoAlunoStatus Status,
    IReadOnlyList<TreinoExercicioResponse> Exercicios);

public record ListarFichasAlunoResponse(
    IReadOnlyList<FichaAlunoResponse> Items,
    int Total,
    int Pagina,
    int TamanhoPagina);

public class ListarFichasAlunoHandler(ITreinoAlunoRepository treinoAlunoRepository)
{
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;

    public virtual async Task<ListarFichasAlunoResponse> HandleAsync(
        Guid alunoId,
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _treinoAlunoRepository
            .ListarDetalhesPorAlunoAsync(alunoId, pagina, tamanhoPagina, cancellationToken)
            .ConfigureAwait(false);

        var response = items.Select(x => new FichaAlunoResponse(
            x.TreinoAluno.Id,
            x.Treino.Id,
            x.Treino.Nome,
            x.Treino.Objetivo,
            x.TreinoAluno.Status,
            x.Treino.Exercicios.Select(te => new TreinoExercicioResponse(
                te.Id,
                te.ExercicioId,
                te.Exercicio?.Nome ?? string.Empty,
                te.Series.Select(s => new SerieConfigResponse(
                    s.Id, s.Quantidade, s.RepeticoesMin, s.RepeticoesMax,
                    s.Descricao, s.Carga, s.Descanso, s.Ordem)).ToList(),
                te.Ordem)).ToList())).ToList();

        return new ListarFichasAlunoResponse(response, total, pagina, tamanhoPagina);
    }
}
