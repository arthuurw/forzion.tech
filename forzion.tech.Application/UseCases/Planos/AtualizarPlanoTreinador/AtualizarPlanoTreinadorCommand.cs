namespace forzion.tech.Application.UseCases.Planos.AtualizarPlanoTreinador;

public record AtualizarPlanoTreinadorCommand(Guid PlanoId, string? Nome, int? MaxAlunos, decimal? Preco);
