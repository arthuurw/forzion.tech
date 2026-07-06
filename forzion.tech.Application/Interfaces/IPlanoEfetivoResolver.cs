using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces;

public record PlanoEfetivo(Guid? PlanoId, TierPlano Tier, int MaxAlunos, bool EhFree);

public interface IPlanoEfetivoResolver
{
    /// <summary>
    /// Resolve o plano efetivo do treinador: MAX (por Preco) entre a assinatura Ativa e o plano de cortesia;
    /// na ausência de ambos, o plano Free canônico. Fail-closed (cap 0, sem features pagas) se nem o Free
    /// estiver configurado na plataforma.
    /// </summary>
    Task<PlanoEfetivo> ResolverAsync(Guid treinadorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mesma resolução, para chamadores que já têm o <see cref="Treinador"/> carregado — evita o
    /// round trip redundante de <see cref="ResolverAsync(Guid, CancellationToken)"/>.
    /// </summary>
    Task<PlanoEfetivo> ResolverAsync(Treinador treinador, CancellationToken cancellationToken = default);
}
