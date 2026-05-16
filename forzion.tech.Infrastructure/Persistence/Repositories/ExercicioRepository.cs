using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ExercicioRepository(AppDbContext context) : IExercicioRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Exercicio?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Exercicios
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<Exercicio> Items, int Total)> ListarAsync(
        Guid? treinadorId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default,
        string? nome = null, forzion.tech.Domain.Enums.TipoGrupoMuscular? grupoMuscular = null, string ordenarPor = "nome")
    {
        var query = _context.Exercicios
            .AsNoTracking()
            .Where(e => e.TreinadorId == treinadorId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(e => e.Nome.ToLower().Contains(nome.ToLower()));

        if (grupoMuscular.HasValue)
            query = query.Where(e => e.GrupoMuscular == grupoMuscular.Value);

        query = ordenarPor == "grupoMuscular"
            ? query.OrderBy(e => e.GrupoMuscular).ThenBy(e => e.Nome)
            : query.OrderBy(e => e.Nome);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task AdicionarAsync(Exercicio exercicio, CancellationToken cancellationToken = default) =>
        await _context.Exercicios.AddAsync(exercicio, cancellationToken).ConfigureAwait(false);

    public Task RemoverAsync(Exercicio exercicio, CancellationToken cancellationToken = default)
    {
        _context.Exercicios.Remove(exercicio);
        return Task.CompletedTask;
    }

    public async Task<bool> ExisteAsync(Guid id, Guid? treinadorId, CancellationToken cancellationToken = default) =>
        await _context.Exercicios
            .AnyAsync(e => e.Id == id && (e.TreinadorId == null || e.TreinadorId == treinadorId), cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> NomeJaExisteAsync(string nome, Guid? treinadorId, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        await _context.Exercicios
            .AnyAsync(e => e.TreinadorId == treinadorId
                        && e.Nome.ToLower() == nome.ToLower()
                        && (excludeId == null || e.Id != excludeId), cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> EstaEmUsoAsync(Guid exercicioId, CancellationToken cancellationToken = default) =>
        await _context.TreinoExercicios
            .AnyAsync(te => te.ExercicioId == exercicioId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, string>> ObterNomesPorIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, string>();

        return await _context.Exercicios
            .AsNoTracking()
            .Where(e => idList.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Nome, cancellationToken)
            .ConfigureAwait(false);
    }
}
