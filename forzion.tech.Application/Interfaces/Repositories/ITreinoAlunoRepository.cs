using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITreinoAlunoRepository
{
    Task<TreinoAluno?> ObterAsync(Guid treinoId, Guid alunoId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(TreinoAluno treinoAluno, CancellationToken cancellationToken = default);
}
