using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, string schema = "public") : DbContext(options), IUnitOfWork
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
    public DbSet<TreinoAluno> TreinoAlunos => Set<TreinoAluno>();
    public DbSet<TreinoExercicio> TreinoExercicios => Set<TreinoExercicio>();
    public DbSet<ExecucaoTreino> ExecucoesTreino => Set<ExecucaoTreino>();
    public DbSet<ExecucaoExercicio> ExecucoesExercicio => Set<ExecucaoExercicio>();

    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
