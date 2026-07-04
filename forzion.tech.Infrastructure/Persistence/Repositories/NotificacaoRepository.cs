using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class NotificacaoRepository(AppDbContext context, IDatabaseErrorInspector dbErrorInspector) : INotificacaoRepository
{
    public async Task AdicionarAsync(Notificacao notificacao, CancellationToken cancellationToken = default)
    {
        await context.Notificacoes.AddAsync(notificacao, cancellationToken).ConfigureAwait(false);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (dbErrorInspector.EhViolacaoDeUnicidade(ex))
        {
            context.Entry(notificacao).State = EntityState.Detached;
        }
    }

    public async Task<IReadOnlyList<Notificacao>> ListarPorContaAsync(Guid contaId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Notificacoes
            .AsNoTracking()
            .Where(n => n.DestinatarioContaId == contaId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> ContarNaoLidasAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        context.Notificacoes
            .Where(n => n.DestinatarioContaId == contaId && !n.Lida)
            .CountAsync(cancellationToken);

    public async Task<bool> MarcarLidaAsync(Guid id, Guid contaId, DateTime agora, CancellationToken cancellationToken = default)
    {
        var afetadas = await context.Notificacoes
            .Where(n => n.Id == id && n.DestinatarioContaId == contaId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(n => n.Lida, true).SetProperty(n => n.UpdatedAt, agora),
                cancellationToken)
            .ConfigureAwait(false);
        return afetadas > 0;
    }

    public Task<int> PurgarAntesDeAsync(DateTime limite, CancellationToken cancellationToken = default) =>
        context.Notificacoes
            .Where(n => n.CreatedAt < limite)
            .ExecuteDeleteAsync(cancellationToken);
}
