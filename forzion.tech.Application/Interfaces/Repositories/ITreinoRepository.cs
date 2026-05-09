using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITreinoRepository
{
    Task<Treino?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<(Treino Treino, string? NomeAluno)> Items, int Total)> ListarPorTreinadorAsync(
        Guid treinadorId, int pagina, int tamanhoPagina,
        string? nome = null, string? objetivo = null, string? ordenarPor = null,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Treino> Items, int Total)> ListarPorAlunoAsync(Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Treino treino, CancellationToken cancellationToken = default);
    Task AdicionarTreinoExercicioAsync(TreinoExercicio item, CancellationToken cancellationToken = default);
    Task RemoverAsync(Treino treino, CancellationToken cancellationToken = default);
}
