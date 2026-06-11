using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Outbox;

// Atomicidade do enfileiramento: o efeito durável entra no MESMO SaveChanges/transação
// do agregado de origem — commit persiste os dois; rollback descarta os dois.
[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class OutboxAtomicidadeTests(InfrastructureTestFixture fixture)
{
    private sealed class NoOpDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DispatchDuravelAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ContaDuravelHandlerFake : IDomainEventHandler<ContaRegistradaEvent>
    {
        public Task HandleAsync(ContaRegistradaEvent domainEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static OutboxDurabilityRegistry Registry() =>
        new OutboxDurabilityRegistry()
            .Registrar<ContaRegistradaEvent, ContaDuravelHandlerFake>(e => $"evt:Conta:{e.ContaId}");

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        // Dispatcher não-nulo: a partição só coleta eventos quando há dispatcher (vide CommitAsync).
        return new AppDbContext(options, new NoOpDispatcher(), Registry());
    }

    [Fact]
    public async Task CommitAsync_EventoDuravel_PersisteOutboxNoMesmoSaveChanges()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var conta = Conta.Criar(Email.Criar($"atom{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var chave = $"evt:Conta:{conta.Id}";

        ctx.Contas.Add(conta);
        await ctx.CommitAsync();

        var row = await ctx.OutboxEfeitos.AsNoTracking().SingleAsync(o => o.ChaveIdempotencia == chave);
        row.Tipo.Should().Be($"evt:{typeof(ContaRegistradaEvent).FullName}");
        row.Status.Should().Be(OutboxStatus.Pendente);
    }

    [Fact]
    public async Task CommitAsync_RollbackDaTransacao_DescartaAgregadoEOutbox()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var conta = Conta.Criar(Email.Criar($"atomrb{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var chave = $"evt:Conta:{conta.Id}";

        await using (var tx = await ctx.Database.BeginTransactionAsync())
        {
            ctx.Contas.Add(conta);
            await ctx.CommitAsync();          // SaveChanges dentro da tx — não commita a tx
            await tx.RollbackAsync();
        }

        // Contexto novo: lê do banco, não do change tracker da transação revertida.
        await using var verify = CreateContext();
        (await verify.OutboxEfeitos.AsNoTracking().AnyAsync(o => o.ChaveIdempotencia == chave))
            .Should().BeFalse("rollback do agregado deve descartar o outbox enfileirado junto");
        (await verify.Contas.AsNoTracking().AnyAsync(c => c.Id == conta.Id))
            .Should().BeFalse();
    }
}
