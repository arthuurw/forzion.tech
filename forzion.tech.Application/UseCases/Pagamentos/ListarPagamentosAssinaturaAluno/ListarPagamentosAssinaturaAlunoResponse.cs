using forzion.tech.Application.UseCases.Pagamentos;

namespace forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinaturaAluno;

public record ListarPagamentosAssinaturaAlunoResponse(
    IReadOnlyList<PagamentoResponse> Items,
    int Total,
    int Pagina,
    int TamanhoPagina);
