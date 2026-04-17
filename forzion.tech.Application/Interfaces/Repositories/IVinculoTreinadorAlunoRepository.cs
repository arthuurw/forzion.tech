using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IVinculoTreinadorAlunoRepository
{
    Task<VinculoTreinadorAluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VinculoTreinadorAluno?> ObterAtivoAsync(Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default);
    Task<VinculoTreinadorAluno?> ObterAtivoPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VinculoTreinadorAluno>> ListarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task<int> ContarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(VinculoTreinadorAluno vinculo, CancellationToken cancellationToken = default);
}
