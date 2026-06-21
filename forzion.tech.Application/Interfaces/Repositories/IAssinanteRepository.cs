using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IAssinanteRepository
{
    Task<Assinante?> ObterPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Assinante assinante, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scrubs nome and email on the Assinante read-model for the given aluno
    /// (LGPD anonymization). No-op if record does not exist.
    /// </summary>
    Task AnonimizarPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task ExcluirPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default);
}
