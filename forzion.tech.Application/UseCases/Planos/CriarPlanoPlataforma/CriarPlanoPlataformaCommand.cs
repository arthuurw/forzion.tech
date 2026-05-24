using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Planos.CriarPlanoPlataforma;

public record CriarPlanoPlataformaCommand(string Nome, TierPlano Tier, int MaxAlunos, decimal Preco, string? Descricao = null);
