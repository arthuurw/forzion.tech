using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IPacoteAlunoRepository
{
    Task<PacoteAluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PacoteAluno>> ListarPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(PacoteAluno pacote, CancellationToken cancellationToken = default);
}
