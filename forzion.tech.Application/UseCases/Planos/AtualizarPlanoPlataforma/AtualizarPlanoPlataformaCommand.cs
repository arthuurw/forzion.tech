using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Planos.AtualizarPlanoPlataforma;

public record AtualizarPlanoPlataformaCommand(Guid PlanoId, string? Nome, TierPlano? Tier, int? MaxAlunos, decimal? Preco, string? Descricao = null);
