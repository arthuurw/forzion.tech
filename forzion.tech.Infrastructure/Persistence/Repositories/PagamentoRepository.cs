using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class PagamentoRepository(AppDbContext context) : IPagamentoRepository
{
    public async Task<Pagamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Pagamentos
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Pagamento?> ObterPorPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken = default) =>
        await context.Pagamentos
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Pagamento>> ListarPorAssinaturaAlunoAsync(Guid assinaturaId, CancellationToken cancellationToken = default) =>
        await context.Pagamentos
            .AsNoTracking()
            .Where(p => p.AssinaturaAlunoId == assinaturaId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<Pagamento> Items, int Total)> ListarPorAssinaturaAlunoPaginadoAsync(
        Guid assinaturaId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
    {
        var baseQuery = context.Pagamentos
            .AsNoTracking()
            .Where(p => p.AssinaturaAlunoId == assinaturaId);

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await baseQuery
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task<Pagamento?> ObterPendentePorAssinaturaAlunoAsync(Guid assinaturaId, CancellationToken cancellationToken = default) =>
        await context.Pagamentos
            .FirstOrDefaultAsync(p => p.AssinaturaAlunoId == assinaturaId && p.Status == PagamentoStatus.Pendente, cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(Pagamento pagamento, CancellationToken cancellationToken = default) =>
        await context.Pagamentos.AddAsync(pagamento, cancellationToken).ConfigureAwait(false);

    public async Task<int> ContarPorStatusAsync(PagamentoStatus status, CancellationToken cancellationToken = default) =>
        await context.Pagamentos
            .CountAsync(p => p.Status == status, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<RecebimentoTreinadorItem>> ListarPorTreinadorAsync(
        Guid treinadorId, DateTime? cursorCreatedAt, Guid? cursorId, int limite,
        CancellationToken cancellationToken = default) =>
        await (
            from p in context.Pagamentos.AsNoTracking()
            join a in context.AssinaturaAlunos.AsNoTracking() on p.AssinaturaAlunoId equals a.Id
            join al in context.Alunos.AsNoTracking() on a.AlunoId equals al.Id
            where a.TreinadorId == treinadorId
                  && (cursorCreatedAt == null
                      || p.CreatedAt < cursorCreatedAt
                      || (p.CreatedAt == cursorCreatedAt && p.Id < cursorId))
            orderby p.CreatedAt descending, p.Id descending
            select new RecebimentoTreinadorItem(
                p.Id, p.Valor, p.Status, p.MetodoPagamento, al.Nome, p.CreatedAt, p.DataPagamento))
            .Take(limite)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
