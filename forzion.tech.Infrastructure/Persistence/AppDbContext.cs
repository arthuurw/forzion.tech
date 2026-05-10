using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, string schema = "public", IDomainEventDispatcher? eventDispatcher = null) : DbContext(options), IUnitOfWork
{
    private readonly string _schema = schema;

    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<SystemUser> SystemUsers => Set<SystemUser>();
    public DbSet<Treinador> Treinadores => Set<Treinador>();
    public DbSet<PlanoTreinador> PlanosTreinador => Set<PlanoTreinador>();
    public DbSet<PacoteAluno> PacotesAluno => Set<PacoteAluno>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<VinculoTreinadorAluno> VinculosTreinadorAluno => Set<VinculoTreinadorAluno>();
    public DbSet<LogAprovacao> LogsAprovacao => Set<LogAprovacao>();
    public DbSet<Treino> Treinos => Set<Treino>();
    public DbSet<Exercicio> Exercicios => Set<Exercicio>();
    public DbSet<GrupoMuscular> GruposMusculares => Set<GrupoMuscular>();
    public DbSet<TreinoAluno> TreinoAlunos => Set<TreinoAluno>();
    public DbSet<TreinoExercicio> TreinoExercicios => Set<TreinoExercicio>();
    public DbSet<ExecucaoTreino> ExecucoesTreino => Set<ExecucaoTreino>();
    public DbSet<ExecucaoExercicio> ExecucoesExercicio => Set<ExecucaoExercicio>();
    public DbSet<TokenRevogado> TokensRevogados => Set<TokenRevogado>();

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = eventDispatcher is null
            ? []
            : ChangeTracker.Entries<IHasDomainEvents>()
                .Select(e => e.Entity)
                .Where(e => e.DomainEvents.Count > 0)
                .ToList();

        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var entity in entitiesWithEvents)
        {
            await eventDispatcher!.DispatchAsync(entity.DomainEvents, cancellationToken).ConfigureAwait(false);
            entity.ClearDomainEvents();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
