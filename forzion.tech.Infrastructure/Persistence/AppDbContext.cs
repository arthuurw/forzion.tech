using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence;

public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly string _schema;

    public AppDbContext(DbContextOptions<AppDbContext> options, string schema = "public")
        : base(options)
    {
        _schema = schema;
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Plano> Planos => Set<Plano>();

    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
