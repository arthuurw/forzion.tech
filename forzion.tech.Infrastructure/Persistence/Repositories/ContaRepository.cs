using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ContaRepository(AppDbContext context) : IContaRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Conta?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default) =>
        await _context.Contas
            .FirstOrDefaultAsync(c => EF.Property<string>(c, "Email") == email, cancellationToken)
            .ConfigureAwait(false);
}
