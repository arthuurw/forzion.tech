using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _context;

    public UsuarioRepository(AppDbContext context) => _context = context;

    public async Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Usuarios
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> ExisteAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Usuarios.AnyAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default) =>
        await _context.Usuarios.AddAsync(usuario, cancellationToken).ConfigureAwait(false);
}
