using System.Data;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class AppDbContextBestEffortPosCommitTests(InfrastructureTestFixture fixture)
{
    private AppDbContext CreateContext(IDomainEventDispatcher dispatcher)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, dispatcher);
    }

    [Fact]
    public async Task CommitAsync_DentroDeTxExplicita_DespachaBestEffortSoAposCommitDaTx()
    {
        var dispatcher = new DispatcherContador();
        await using var ctx = CreateContext(dispatcher);
        await ctx.Database.EnsureCreatedAsync();

        var (conta, aluno, esperados) = NovoAluno();

        await using var tx = await ctx.BeginTransactionAsync(IsolationLevel.Serializable);
        await ctx.Contas.AddAsync(conta);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.CommitAsync();

        dispatcher.TotalDespachado.Should().Be(0,
            "o best-effort não pode disparar antes do commit real da transação externa");

        await tx.CommitAsync();

        dispatcher.TotalDespachado.Should().Be(esperados,
            "após o commit da tx externa cada evento best-effort dispara uma vez");
    }

    [Fact]
    public async Task CommitAsync_TxDescartadaSemCommit_NaoDespachaBestEffort()
    {
        var dispatcher = new DispatcherContador();
        await using var ctx = CreateContext(dispatcher);
        await ctx.Database.EnsureCreatedAsync();

        var (conta, aluno, _) = NovoAluno();

        await using (var tx = await ctx.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await ctx.Contas.AddAsync(conta);
            await ctx.Alunos.AddAsync(aluno);
            await ctx.CommitAsync();
        }

        dispatcher.TotalDespachado.Should().Be(0,
            "transação descartada sem commit não pode disparar os eventos enfileirados");
    }

    private static (Conta, Aluno, int) NovoAluno()
    {
        var conta = Conta.Criar(Email.Criar($"poscommit{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(conta.Id, "PosCommit", DateTime.UtcNow).Value;
        var esperados = conta.DomainEvents.Count + aluno.DomainEvents.Count;
        esperados.Should().BeGreaterThan(0);
        return (conta, aluno, esperados);
    }

    private sealed class DispatcherContador : IDomainEventDispatcher
    {
        public int TotalDespachado { get; private set; }

        public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            TotalDespachado += events.Count;
            return Task.CompletedTask;
        }

        public Task DispatchDuravelAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
