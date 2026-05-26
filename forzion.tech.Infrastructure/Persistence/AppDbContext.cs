using System.Data;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace forzion.tech.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, IDomainEventDispatcher? eventDispatcher = null) : DbContext(options), IUnitOfWork, IDbContextTransactionProvider
{

    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<SystemUser> SystemUsers => Set<SystemUser>();
    public DbSet<Treinador> Treinadores => Set<Treinador>();
    public DbSet<PlanoPlataforma> PlanosPlataforma => Set<PlanoPlataforma>();
    public DbSet<Pacote> Pacotes => Set<Pacote>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<VinculoTreinadorAluno> VinculosTreinadorAluno => Set<VinculoTreinadorAluno>();
    public DbSet<LogAprovacao> LogsAprovacao => Set<LogAprovacao>();
    public DbSet<Treino> Treinos => Set<Treino>();
    public DbSet<Exercicio> Exercicios => Set<Exercicio>();
    public DbSet<GrupoMuscular> GruposMusculares => Set<GrupoMuscular>();
    public DbSet<TreinoAluno> TreinoAlunos => Set<TreinoAluno>();
    internal DbSet<TreinoExercicio> TreinoExercicios => Set<TreinoExercicio>();
    public DbSet<ExecucaoTreino> ExecucoesTreino => Set<ExecucaoTreino>();
    internal DbSet<ExecucaoExercicio> ExecucoesExercicio => Set<ExecucaoExercicio>();
    public DbSet<TokenRevogado> TokensRevogados => Set<TokenRevogado>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<EmailDeliveryLog> EmailDeliveryLogs => Set<EmailDeliveryLog>();
    public DbSet<AssinaturaAluno> AssinaturaAlunos => Set<AssinaturaAluno>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<Assinante> Assinantes => Set<Assinante>();
    public DbSet<ContaRecebimento> ContasRecebimento => Set<ContaRecebimento>();
    public DbSet<HealthReportConfig> HealthReportConfigs => Set<HealthReportConfig>();
    public DbSet<HealthSnapshot> HealthSnapshots => Set<HealthSnapshot>();
    public DbSet<ErrorLogEntry> ErrorLogs => Set<ErrorLogEntry>();

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = eventDispatcher is null
            ? []
            : ChangeTracker.Entries<IHasDomainEvents>()
                .Select(e => e.Entity)
                .Where(e => e.DomainEvents.Count > 0)
                .ToList();

        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Snapshot + limpa ANTES de despachar. Handlers podem chamar CommitAsync de
        // novo (re-entrância); se os eventos ainda estivessem na entidade, o commit
        // aninhado os re-coletaria e re-despacharia (ex.: projeção Assinante inserida
        // 2x → duplicate key). Limpar antes garante "dispara cada evento uma vez".
        var domainEvents = new List<IDomainEvent>();
        foreach (var entity in entitiesWithEvents)
        {
            domainEvents.AddRange(entity.DomainEvents);
            entity.ClearDomainEvents();
        }

        if (domainEvents.Count > 0)
            await eventDispatcher!.DispatchAsync(domainEvents, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ITransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        var tx = await Database.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
        return new EfCoreTransactionAdapter(tx);
    }

    private sealed class EfCoreTransactionAdapter(IDbContextTransaction inner) : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            inner.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema-agnostic: o schema-alvo vem do search_path da connection string
        // (ex.: "Search Path=homolog"), não de HasDefaultSchema. Assim as mesmas
        // migrations aplicam em qualquer schema (homolog, develop, public).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
