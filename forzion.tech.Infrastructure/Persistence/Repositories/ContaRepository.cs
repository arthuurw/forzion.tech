using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ContaRepository(AppDbContext context) : IContaRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Conta?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Contas
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Conta?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var emailVo = Email.FromDatabase(email);
        return await _context.Contas
            .FirstOrDefaultAsync(c => c.Email == emailVo, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(Conta conta, CancellationToken cancellationToken = default) =>
        await _context.Contas.AddAsync(conta, cancellationToken).ConfigureAwait(false);

    public async Task<int> ContarCriadasDesdeAsync(DateTime desde, CancellationToken cancellationToken = default) =>
        await _context.Contas
            .CountAsync(c => c.CreatedAt >= desde, cancellationToken)
            .ConfigureAwait(false);
}
