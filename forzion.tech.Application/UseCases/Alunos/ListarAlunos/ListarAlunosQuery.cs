namespace forzion.tech.Application.UseCases.Alunos.ListarAlunos;

public record ListarAlunosQuery(Guid TenantId, int Pagina = 1, int TamanhoPagina = 20);
