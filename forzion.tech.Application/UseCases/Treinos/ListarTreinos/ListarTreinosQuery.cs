namespace forzion.tech.Application.UseCases.Treinos.ListarTreinos;

public record ListarTreinosQuery(Guid TenantId, Guid AlunoId, int Pagina, int TamanhoPagina);
