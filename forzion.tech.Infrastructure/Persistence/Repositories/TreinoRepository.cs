using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TreinoRepository(AppDbContext context) : ITreinoRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Treino?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Treinos
            .Include(t => t.Exercicios).ThenInclude(te => te.Series)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<(Treino Treino, string? NomeAluno)> Items, int Total)> ListarPorTreinadorAsync(
        Guid treinadorId, int pagina, int tamanhoPagina,
        string? nome = null, string? objetivo = null, string? ordenarPor = null,
        CancellationToken cancellationToken = default)
    {
        var q = from t in _context.Treinos.AsNoTracking()
                where t.TreinadorId == treinadorId
                join ta in _context.TreinoAlunos.Where(x => x.Status == TreinoAlunoStatus.Ativo)
                    on t.Id equals ta.TreinoId into taGroup
                from ta in taGroup.DefaultIfEmpty()
                join a in _context.Alunos on ta.AlunoId equals a.Id into aGroup
                from a in aGroup.DefaultIfEmpty()
                select new { TreinoId = t.Id, NomeAluno = (string?)a.Nome, t.Nome, t.Objetivo, t.CreatedAt };

        if (!string.IsNullOrWhiteSpace(nome))
            q = q.Where(x => x.Nome.ToLower().Contains(nome.ToLower()));

        if (!string.IsNullOrWhiteSpace(objetivo) && Enum.TryParse<ObjetivoTreino>(objetivo, out var obj))
            q = q.Where(x => x.Objetivo == obj);

        var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);

        q = ordenarPor switch
        {
            "objetivo"   => q.OrderBy(x => x.Objetivo).ThenBy(x => x.Nome),
            "createdAt"  => q.OrderByDescending(x => x.CreatedAt),
            "nomeAluno"  => q.OrderBy(x => x.NomeAluno).ThenBy(x => x.Nome),
            _            => q.OrderBy(x => x.Nome),
        };

        var paginated = await q
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var treinoIds = paginated.Select(x => x.TreinoId).ToList();
        var nomeAlunoMap = paginated.ToDictionary(x => x.TreinoId, x => x.NomeAluno);

        var treinos = await _context.Treinos
            .AsNoTracking()
            .Where(t => treinoIds.Contains(t.Id))
            .Include(t => t.Exercicios).ThenInclude(te => te.Series)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var treinoDict = treinos.ToDictionary(t => t.Id);
        var items = treinoIds
            .Where(id => treinoDict.ContainsKey(id))
            .Select(id => (treinoDict[id], nomeAlunoMap.GetValueOrDefault(id)))
            .ToList();

        return (items, total);
    }

    public async Task<(IReadOnlyList<Treino> Items, int Total)> ListarPorAlunoAsync(
        Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var treinoIds = _context.TreinoAlunos
            .AsNoTracking()
            .Where(ta => ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo)
            .Select(ta => ta.TreinoId);

        var baseQuery = _context.Treinos
            .AsNoTracking()
            .Where(t => treinoIds.Contains(t.Id));

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await baseQuery
            .Include(t => t.Exercicios).ThenInclude(te => te.Series)
            .OrderBy(t => t.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task AdicionarAsync(Treino treino, CancellationToken cancellationToken = default) =>
        await _context.Treinos.AddAsync(treino, cancellationToken).ConfigureAwait(false);

    public async Task AdicionarTreinoExercicioAsync(TreinoExercicio item, CancellationToken cancellationToken = default) =>
        await _context.TreinoExercicios.AddAsync(item, cancellationToken).ConfigureAwait(false);

    public Task RemoverAsync(Treino treino, CancellationToken cancellationToken = default)
    {
        _context.Treinos.Remove(treino);
        return Task.CompletedTask;
    }
}
