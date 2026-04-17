namespace forzion.tech.Application.UseCases.Alunos.ListarAlunos;

public record ListarAlunosQuery(Guid TreinadorId, int Pagina = 1, int TamanhoPagina = 20);
