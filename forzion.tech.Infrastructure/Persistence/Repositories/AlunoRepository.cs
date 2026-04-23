using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class AlunoRepository(AppDbContext context) : IAlunoRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Aluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Alunos
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Aluno?> ObterPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await _context.Alunos
            .FirstOrDefaultAsync(a => a.ContaId == contaId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<Aluno> Items, int Total)> ListarPorTreinadorAsync(
        Guid treinadorId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var query = _context.VinculosTreinadorAluno
            .Where(v => v.TreinadorId == treinadorId && v.Status == VinculoStatus.Ativo)
            .Join(_context.Alunos, v => v.AlunoId, a => a.Id, (_, a) => a)
            .OrderBy(a => a.Nome);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task AdicionarAsync(Aluno aluno, CancellationToken cancellationToken = default) =>
        await _context.Alunos.AddAsync(aluno, cancellationToken).ConfigureAwait(false);

    public async Task<int> ContarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await _context.VinculosTreinadorAluno
            .CountAsync(v => v.TreinadorId == treinadorId && v.Status == VinculoStatus.Ativo, cancellationToken)
            .ConfigureAwait(false);
}
