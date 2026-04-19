using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public record TreinoAlunoComNome(TreinoAluno TreinoAluno, string NomeTreino);
public record TreinoAlunoDetalhe(TreinoAluno TreinoAluno, Treino Treino);

public interface ITreinoAlunoRepository
{
    Task<TreinoAluno?> ObterAsync(Guid treinoId, Guid alunoId, CancellationToken cancellationToken = default);
    Task<int> ContarAtivosPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreinoAluno>> ListarAtivosPorParAsync(Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreinoAluno>> ListarAtivosPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreinoAlunoComNome>> ListarAtivosComNomePorParAsync(Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreinoAlunoComNome>> ListarAtivosComNomePorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<TreinoAlunoDetalhe> Items, int Total)> ListarDetalhesPorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task<TreinoAlunoComNome?> ObterComNomeAsync(Guid treinoAlunoId, Guid alunoId, CancellationToken cancellationToken = default);
    Task<TreinoAlunoDetalhe?> ObterDetalheAsync(Guid treinoAlunoId, Guid alunoId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(TreinoAluno treinoAluno, CancellationToken cancellationToken = default);
}
