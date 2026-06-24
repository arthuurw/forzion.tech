namespace forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinaturaAluno;

public record ListarPagamentosAssinaturaAlunoQuery(Guid AssinaturaAlunoId, Guid AlunoId, int Pagina = 1, int TamanhoPagina = 20);
