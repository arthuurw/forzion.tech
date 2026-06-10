using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IOutboxRepository
{
    // Adiciona ao ChangeTracker SEM commitar — o CommitAsync do agregado de origem persiste junto (atomicidade).
    void Enfileirar(OutboxEfeito efeito);

    // Lease pessimista: itens elegíveis (Pendente, proxima_tentativa <= agora), travados via
    // FOR UPDATE SKIP LOCKED. Chamar dentro de transação; o lock solta no commit do worker.
    Task<IReadOnlyList<OutboxEfeito>> ObterProcessaveisAsync(int max, DateTime agora, CancellationToken cancellationToken = default);

    // Remove itens Concluido com processado_em < limite (retenção). Delete em lote, sem ChangeTracker.
    Task<int> LimparConcluidosAnterioresAsync(DateTime limite, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<OutboxStatus, int>> ContarPorStatusAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxEfeito>> ListarPorStatusAsync(OutboxStatus status, int max, CancellationToken cancellationToken = default);
}
