using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IExecucaoTreinoRepository
{
    Task AdicionarAsync(ExecucaoTreino execucao, CancellationToken cancellationToken = default);
    Task<bool> ExisteParaTreinoAsync(Guid treinoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecucaoTreino>> ListarPorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task<int> ContarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
}
