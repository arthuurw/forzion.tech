namespace forzion.tech.Application.UseCases.Alunos.ListarAlunos;

public record ListarAlunosResponse(
    IReadOnlyList<AlunoResponse> Items,
    int Total,
    int Pagina,
    int TamanhoPagina
);
