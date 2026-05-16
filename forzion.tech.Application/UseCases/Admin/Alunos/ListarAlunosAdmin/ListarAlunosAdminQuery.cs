using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Admin.Alunos.ListarAlunosAdmin;

public record ListarAlunosAdminQuery(
    int Pagina = 1,
    int TamanhoPagina = 20,
    string? Nome = null,
    AlunoStatus? Status = null);
