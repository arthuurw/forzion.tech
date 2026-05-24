using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class AssinanteRepository(AppDbContext context) : IAssinanteRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Assinante?> ObterPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.Assinantes
            .FirstOrDefaultAsync(a => a.AlunoId == alunoId, cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(Assinante assinante, CancellationToken cancellationToken = default) =>
        await _context.Assinantes.AddAsync(assinante, cancellationToken).ConfigureAwait(false);
}
