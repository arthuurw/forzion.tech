using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ContaRepository(AppDbContext context) : IContaRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Conta?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var contas = await _context.Contas.ToListAsync(cancellationToken).ConfigureAwait(false);
        return contas.FirstOrDefault(c => c.Email.Value == email);
    }

    public async Task AdicionarAsync(Conta conta, CancellationToken cancellationToken = default) =>
        await _context.Contas.AddAsync(conta, cancellationToken).ConfigureAwait(false);
}
