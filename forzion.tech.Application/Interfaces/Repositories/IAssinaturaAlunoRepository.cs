using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IAssinaturaAlunoRepository
{
    Task<AssinaturaAluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AssinaturaAluno?> ObterPorVinculoIdAsync(Guid vinculoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssinaturaAluno>> ListarParaRenovarAsync(DateTime ate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssinaturaAluno>> ListarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(AssinaturaAluno assinatura, CancellationToken cancellationToken = default);
}
