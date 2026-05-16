using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IExercicioRepository
{
    Task<Exercicio?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Exercicio> Items, int Total)> ListarAsync(
        Guid? treinadorId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default,
        string? nome = null, forzion.tech.Domain.Enums.TipoGrupoMuscular? grupoMuscular = null, string ordenarPor = "nome");
    Task AdicionarAsync(Exercicio exercicio, CancellationToken cancellationToken = default);
    Task RemoverAsync(Exercicio exercicio, CancellationToken cancellationToken = default);
    Task<bool> ExisteAsync(Guid id, Guid? treinadorId, CancellationToken cancellationToken = default);
    Task<bool> NomeJaExisteAsync(string nome, Guid? treinadorId, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> EstaEmUsoAsync(Guid exercicioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, string>> ObterNomesPorIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
