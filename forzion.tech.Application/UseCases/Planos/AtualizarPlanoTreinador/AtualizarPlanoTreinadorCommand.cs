using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Planos.AtualizarPlanoTreinador;

public record AtualizarPlanoTreinadorCommand(Guid PlanoId, string? Nome, TierPlano? Tier, int? MaxAlunos, decimal? Preco);
