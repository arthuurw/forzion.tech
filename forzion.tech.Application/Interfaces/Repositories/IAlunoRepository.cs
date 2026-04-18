using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IAlunoRepository
{
    Task<Aluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Aluno?> ObterPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Aluno> Items, int Total)> ListarPorTreinadorAsync(Guid treinadorId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Aluno aluno, CancellationToken cancellationToken = default);
    Task<int> ContarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
}
