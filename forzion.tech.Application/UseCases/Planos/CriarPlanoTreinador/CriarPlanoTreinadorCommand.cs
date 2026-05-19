using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Planos.CriarPlanoTreinador;

public record CriarPlanoTreinadorCommand(string Nome, TierPlano Tier, int MaxAlunos, decimal Preco, string? Descricao = null);
