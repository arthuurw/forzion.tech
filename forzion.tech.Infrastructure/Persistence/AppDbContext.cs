using System.Data;
using System.Text.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace forzion.tech.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IDomainEventDispatcher? eventDispatcher = null,
    OutboxDurabilityRegistry? outboxDurabilidade = null) : DbContext(options), IUnitOfWork, IDbContextTransactionProvider, IDataProtectionKeyContext
{

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

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
    public DbSet<RefreshTokenFamily> RefreshTokenFamilies => Set<RefreshTokenFamily>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<EmailDeliveryLog> EmailDeliveryLogs => Set<EmailDeliveryLog>();
    public DbSet<WhatsAppDeliveryLog> WhatsAppDeliveryLogs => Set<WhatsAppDeliveryLog>();
    public DbSet<AssinaturaAluno> AssinaturaAlunos => Set<AssinaturaAluno>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<AssinaturaTreinador> AssinaturasTreinador => Set<AssinaturaTreinador>();
    public DbSet<PagamentoTreinador> PagamentosTreinador => Set<PagamentoTreinador>();
    public DbSet<Assinante> Assinantes => Set<Assinante>();
    public DbSet<ContaRecebimento> ContasRecebimento => Set<ContaRecebimento>();
    public DbSet<HealthReportConfig> HealthReportConfigs => Set<HealthReportConfig>();
    public DbSet<HealthSnapshot> HealthSnapshots => Set<HealthSnapshot>();
    public DbSet<ErrorLogEntry> ErrorLogs => Set<ErrorLogEntry>();
    public DbSet<OutboxEfeito> OutboxEfeitos => Set<OutboxEfeito>();
    public DbSet<MensagemSuporte> MensagensSuporte => Set<MensagemSuporte>();
    public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();
    public DbSet<ContaMfa> ContasMfa => Set<ContaMfa>();
    public DbSet<MfaRecoveryCode> MfaRecoveryCodes => Set<MfaRecoveryCode>();
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();
    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();
    public DbSet<TrocaEmailToken> TrocaEmailTokens => Set<TrocaEmailToken>();

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = eventDispatcher is null
            ? []
            : ChangeTracker.Entries<IHasDomainEvents>()
                .Select(e => e.Entity)
                .Where(e => e.DomainEvents.Count > 0)
                .ToList();

        // Snapshot + limpa ANTES do SaveChanges. (1) re-entrância: handler que chama
        // CommitAsync aninhado não re-coleta os mesmos eventos (ex.: projeção Assinante
        // inserida 2x → duplicate key). (2) durabilidade: o efeito durável precisa entrar
        // no MESMO SaveChanges do agregado (atomicidade), por isso enfileira aqui.
        var domainEvents = new List<IDomainEvent>();
        foreach (var entity in entitiesWithEvents)
        {
            domainEvents.AddRange(entity.DomainEvents);
            entity.ClearDomainEvents();
        }

        if (outboxDurabilidade is not null)
            foreach (var evento in domainEvents.Where(e => outboxDurabilidade.EhDuravel(e.GetType())))
                OutboxEfeitos.Add(CriarEfeitoDuravel(evento, outboxDurabilidade));

        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (domainEvents.Count > 0 && eventDispatcher is not null)
            await eventDispatcher.DispatchAsync(domainEvents, cancellationToken).ConfigureAwait(false);
    }

    public void DescartarAlteracoesPendentes() => ChangeTracker.Clear();

    private static OutboxEfeito CriarEfeitoDuravel(IDomainEvent evento, OutboxDurabilityRegistry registry)
    {
        // tipo = evt:<FullName> → o worker resolve o tipo CLR pelo registry e desserializa.
        var tipo = $"evt:{evento.GetType().FullName}";
        var payload = JsonSerializer.Serialize(evento, evento.GetType());
        var resultado = OutboxEfeito.Criar(tipo, payload, registry.ChaveIdempotencia(evento), evento.OcorridoEm);
        if (resultado.IsFailure)
            throw new InvalidOperationException($"Falha ao enfileirar efeito durável: {resultado.Error!.Message}");
        return resultado.Value;
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
