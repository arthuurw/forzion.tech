using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class AlunoRepository : IAlunoRepository
{
    private readonly AppDbContext _context;

    public AlunoRepository(AppDbContext context) => _context = context;

    public async Task<Aluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Alunos
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<Aluno> Items, int Total)> ListarAsync(
        Guid tenantId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var query = _context.Alunos
            .Where(a => a.TenantId == tenantId)
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

    public async Task InativarPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await _context.Alunos
            .Where(a => a.TreinadorId == treinadorId && a.Status == AlunoStatus.Ativo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, AlunoStatus.Inativo)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
}
