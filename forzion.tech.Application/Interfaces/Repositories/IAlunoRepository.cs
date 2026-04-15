using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IAlunoRepository
{
    Task<Aluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Aluno> Items, int Total)> ListarAsync(Guid tenantId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Aluno aluno, CancellationToken cancellationToken = default);
    Task InativarPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
}
