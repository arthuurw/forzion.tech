using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TreinoAlunoRepository(AppDbContext context) : ITreinoAlunoRepository
{
    private readonly AppDbContext _context = context;

    public async Task<TreinoAluno?> ObterAsync(Guid treinoId, Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos
            .FirstOrDefaultAsync(ta => ta.TreinoId == treinoId && ta.AlunoId == alunoId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> ContarAtivosPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos
            .CountAsync(ta => ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TreinoAluno>> ListarAtivosPorParAsync(Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos
            .Where(ta => ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo &&
                         _context.Treinos.Any(t => t.Id == ta.TreinoId && t.TreinadorId == treinadorId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TreinoAluno>> ListarAtivosPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos
            .Where(ta => ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TreinoAlunoComNome>> ListarAtivosComNomePorParAsync(
        Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default)
    {
        var raw = await (
            from ta in _context.TreinoAlunos
            join t in _context.Treinos on ta.TreinoId equals t.Id
            where ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo && t.TreinadorId == treinadorId
            select new { ta, t.Nome }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        return raw.Select(x => new TreinoAlunoComNome(x.ta, x.Nome)).ToList();
    }

    public async Task<IReadOnlyList<TreinoAlunoComNome>> ListarAtivosComNomePorAlunoAsync(
        Guid alunoId, CancellationToken cancellationToken = default)
    {
        var raw = await (
            from ta in _context.TreinoAlunos
            join t in _context.Treinos on ta.TreinoId equals t.Id
            where ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo
            select new { ta, t.Nome }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        return raw.Select(x => new TreinoAlunoComNome(x.ta, x.Nome)).ToList();
    }

    public async Task<TreinoAlunoComNome?> ObterComNomeAsync(
        Guid treinoAlunoId, Guid alunoId, CancellationToken cancellationToken = default)
    {
        var raw = await (
            from ta in _context.TreinoAlunos
            join t in _context.Treinos on ta.TreinoId equals t.Id
            where ta.Id == treinoAlunoId && ta.AlunoId == alunoId
            select new { ta, t.Nome }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return raw is null ? null : new TreinoAlunoComNome(raw.ta, raw.Nome);
    }

    public async Task<(IReadOnlyList<TreinoAlunoDetalhe> Items, int Total)> ListarDetalhesPorAlunoAsync(
        Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.TreinoAlunos
            .Where(ta => ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo);

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await baseQuery
            .OrderByDescending(ta => ta.CreatedAt)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Join(
                _context.Treinos
                    .Include(t => t.Exercicios).ThenInclude(te => te.Exercicio)
                    .Include(t => t.Exercicios).ThenInclude(te => te.Series),
                ta => ta.TreinoId,
                t => t.Id,
                (ta, t) => new TreinoAlunoDetalhe(ta, t))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task<TreinoAlunoDetalhe?> ObterDetalheAsync(
        Guid treinoAlunoId, Guid alunoId, CancellationToken cancellationToken = default)
    {
        var detail = await _context.TreinoAlunos
            .Where(ta => ta.Id == treinoAlunoId && ta.AlunoId == alunoId)
            .Join(
                _context.Treinos
                    .Include(t => t.Exercicios).ThenInclude(te => te.Exercicio)
                    .Include(t => t.Exercicios).ThenInclude(te => te.Series),
                ta => ta.TreinoId,
                t => t.Id,
                (ta, t) => new TreinoAlunoDetalhe(ta, t))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return detail;
    }

    public async Task<TreinoAlunoDetalhe?> ObterDetalheAdminAsync(
        Guid treinoAlunoId, CancellationToken cancellationToken = default)
    {
        var detail = await _context.TreinoAlunos
            .Where(ta => ta.Id == treinoAlunoId)
            .Join(
                _context.Treinos
                    .Include(t => t.Exercicios).ThenInclude(te => te.Exercicio)
                    .Include(t => t.Exercicios).ThenInclude(te => te.Series),
                ta => ta.TreinoId,
                t => t.Id,
                (ta, t) => new TreinoAlunoDetalhe(ta, t))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return detail;
    }

    public async Task AdicionarAsync(TreinoAluno treinoAluno, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos.AddAsync(treinoAluno, cancellationToken).ConfigureAwait(false);

    public async Task RemoverPorTreinoIdAsync(Guid treinoId, CancellationToken cancellationToken = default)
    {
        var items = await _context.TreinoAlunos
            .Where(ta => ta.TreinoId == treinoId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _context.TreinoAlunos.RemoveRange(items);
    }

    public async Task<IReadOnlyList<TreinoAlunoVinculado>> ListarAtivosPorTreinoIdAsync(
        Guid treinoId, CancellationToken cancellationToken = default)
    {
        var raw = await (
            from ta in _context.TreinoAlunos
            join a in _context.Alunos on ta.AlunoId equals a.Id
            where ta.TreinoId == treinoId && ta.Status == TreinoAlunoStatus.Ativo
            select new { ta.Id, ta.AlunoId, a.Nome, ta.Status }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        return raw.Select(x => new TreinoAlunoVinculado(x.Id, x.AlunoId, x.Nome, x.Status)).ToList();
    }
}
