using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IAssinaturaRepository
{
    Task<Assinatura?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Assinatura?> ObterPorVinculoIdAsync(Guid vinculoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Assinatura>> ListarParaRenovarAsync(DateTime ate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Assinatura>> ListarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Assinatura assinatura, CancellationToken cancellationToken = default);
}
