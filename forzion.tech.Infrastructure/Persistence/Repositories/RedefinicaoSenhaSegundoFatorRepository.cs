using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class RedefinicaoSenhaSegundoFatorRepository(AppDbContext context) : IRedefinicaoSenhaSegundoFatorRepository
{
    public async Task<RedefinicaoSenhaSegundoFator?> BuscarPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await context.RedefinicoesSenhaSegundoFator
            .FirstOrDefaultAsync(g => g.ContaId == contaId, cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(RedefinicaoSenhaSegundoFator guard, CancellationToken cancellationToken = default) =>
        await context.RedefinicoesSenhaSegundoFator.AddAsync(guard, cancellationToken).ConfigureAwait(false);

    public async Task ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default)
    {
        var registros = await context.RedefinicoesSenhaSegundoFator
            .Where(g => g.ContaId == contaId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.RedefinicoesSenhaSegundoFator.RemoveRange(registros);
    }
}
