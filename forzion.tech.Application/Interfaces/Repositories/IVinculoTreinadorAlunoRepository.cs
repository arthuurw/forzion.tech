using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public record VinculoComDetalheAluno(
    VinculoTreinadorAluno Vinculo,
    string NomeAluno,
    string? EmailAluno,
    bool TemVinculoAtivoPrevio);

public record ReceitaPorPacote(Guid PacoteId, string Nome, int Alunos, decimal Receita);

public interface IVinculoTreinadorAlunoRepository
{
    Task<VinculoTreinadorAluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VinculoTreinadorAluno?> ObterAtivoAsync(Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default);
    Task<VinculoTreinadorAluno?> ObterAtivoPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<(bool Ativo, bool Pendente)> ObterResumoVinculoPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VinculoTreinadorAluno>> ListarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vínculos Ativo do treinador ordenados por <c>CreatedAt</c> asc, <c>Id</c> asc (determinístico) —
    /// usado pela apara de graça de limite de alunos (GRACE-05) para decidir quem preservar.
    /// </summary>
    Task<IReadOnlyList<VinculoTreinadorAluno>> ListarAtivosPorTreinadorOrdenadoAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<VinculoComDetalheAluno> Items, int Total)> ListarComDetalhesAsync(Guid treinadorId, VinculoStatus? status, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task<int> ContarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<VinculoStatus, int>> ContarPorStatusAsync(Guid treinadorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>SUM(pacote.preco) GROUP BY pacote</c> sobre o conjunto inteiro de vínculos ativos, sem
    /// paginação nem clamp — a soma cobre o conjunto completo, não uma página.
    /// </summary>
    Task<IReadOnlyList<ReceitaPorPacote>> SomarReceitaPorPacoteAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task<bool> TemVinculosAtivosAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task<VinculoTreinadorAluno?> ObterPendentePorParAsync(Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default);
    Task<VinculoTreinadorAluno?> ObterPendentePorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(VinculoTreinadorAluno vinculo, CancellationToken cancellationToken = default);
    Task ExcluirPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default);

    /// <summary>Vínculos Ativo + AguardandoAprovacao do aluno, rastreados para anonimização.</summary>
    Task<IReadOnlyList<VinculoTreinadorAluno>> ListarAtivosEPendentesPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VinculoTreinadorAluno>> ListarTodosPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VinculoTreinadorAluno>> ListarTodosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
}
