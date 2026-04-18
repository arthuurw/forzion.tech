using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITreinoAlunoRepository
{
    Task<TreinoAluno?> ObterAsync(Guid treinoId, Guid alunoId, CancellationToken cancellationToken = default);
    Task<int> ContarAtivosPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreinoAluno>> ListarAtivosPorParAsync(Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreinoAluno>> ListarAtivosPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(TreinoAluno treinoAluno, CancellationToken cancellationToken = default);
}
