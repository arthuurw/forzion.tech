using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
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

    public async Task AdicionarAsync(Treinador treinador, CancellationToken cancellationToken = default) =>
        await _context.Treinadores.AddAsync(treinador, cancellationToken).ConfigureAwait(false);
}
