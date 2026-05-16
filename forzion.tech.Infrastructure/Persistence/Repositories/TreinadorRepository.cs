using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TreinadorRepository(AppDbContext context) : ITreinadorRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Treinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Treinadores
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Treinador?> ObterPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await _context.Treinadores
            .FirstOrDefaultAsync(t => t.ContaId == contaId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Treinador>> ListarAtivosAsync(CancellationToken cancellationToken = default) =>
        await _context.Treinadores
            .AsNoTracking()
            .Where(t => t.Status == TreinadorStatus.Ativo)
            .OrderBy(t => t.Nome)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<Treinador> Items, int Total)> ListarAsync(
        TreinadorStatus? status, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var query = _context.Treinadores.AsNoTracking().AsQueryable();
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(t => t.Nome)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task AdicionarAsync(Treinador treinador, CancellationToken cancellationToken = default) =>
        await _context.Treinadores.AddAsync(treinador, cancellationToken).ConfigureAwait(false);

    public async Task<Treinador?> ObterPorStripeAccountIdAsync(string stripeAccountId, CancellationToken cancellationToken = default) =>
        await _context.Treinadores
            .FirstOrDefaultAsync(t => t.StripeConnectAccountId == stripeAccountId, cancellationToken)
            .ConfigureAwait(false);

    public async Task ExcluirComDependenciasAsync(Treinador treinador, CancellationToken cancellationToken = default)
    {
        // All ExecuteDeleteAsync calls share one explicit transaction, so the entire
        // cascade is atomic: if any step fails the transaction is rolled back and no
        // partial deletes are persisted.
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var treinoIds = await _context.Treinos
                .Where(t => t.TreinadorId == treinador.Id)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (treinoIds.Count > 0)
            {
                // ExecucaoExercicio cascades from ExecucaoTreino (ON DELETE CASCADE in DB)
                await _context.ExecucoesTreino
                    .Where(e => treinoIds.Contains(e.TreinoId))
                    .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

                await _context.TreinoAlunos
                    .Where(ta => treinoIds.Contains(ta.TreinoId))
                    .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

                // TreinoExercicio cascades from Treino (ON DELETE CASCADE in DB)
                await _context.Treinos
                    .Where(t => t.TreinadorId == treinador.Id)
                    .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            }

            await _context.Exercicios
                .Where(e => e.TreinadorId == treinador.Id)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            await _context.PacotesAluno
                .Where(p => p.TreinadorId == treinador.Id)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            await _context.VinculosTreinadorAluno
                .Where(v => v.TreinadorId == treinador.Id)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            await _context.Treinadores
                .Where(t => t.Id == treinador.Id)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            await _context.Contas
                .Where(c => c.Id == treinador.ContaId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
