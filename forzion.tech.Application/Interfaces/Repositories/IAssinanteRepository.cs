using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IAssinanteRepository
{
    Task<Assinante?> ObterPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Assinante assinante, CancellationToken cancellationToken = default);
}
