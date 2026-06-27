using System.Data;
using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace forzion.tech.Tests.Infrastructure;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class ExecuteInTransactionNaoRetentaTests(InfrastructureTestFixture fixture)
{
    private AppDbContext CreateContextComRetry()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString, o => o.ExecutionStrategy(
                deps => new AppRetryingExecutionStrategy(deps, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(20))))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_SobStrategyRetentadoraGlobal_CommitaSemErroDeTransacaoManual()
    {
        await using var ctx = CreateContextComRetry();
        await ctx.Database.EnsureCreatedAsync();

        ctx.Database.CreateExecutionStrategy().Should().BeOfType<AppRetryingExecutionStrategy>(
            "a strategy retentadora global precisa estar ativa para este teste valer");

        var conta = Conta.Criar(Email.Criar($"tx{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;

        var contaId = await ctx.ExecuteInTransactionAsync(
            IsolationLevel.ReadCommitted,
            async (tx, ct) =>
            {
                await ctx.Contas.AddAsync(conta, ct);
                await ctx.CommitAsync(ct);
                await tx.CommitAsync(ct);
                return conta.Id;
            });

        (await ctx.Contas.AnyAsync(c => c.Id == contaId)).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_DelegateFalha_ExecutaUmaVezSemRetentar()
    {
        await using var ctx = CreateContextComRetry();
        await ctx.Database.EnsureCreatedAsync();

        var chamadas = 0;

        var act = () => ctx.ExecuteInTransactionAsync<int>(
            IsolationLevel.ReadCommitted,
            (_, _) =>
            {
                chamadas++;
                throw new InvalidOperationException("falha no meio do delegate");
            });

        await act.Should().ThrowAsync<InvalidOperationException>();
        chamadas.Should().Be(1);
    }
}
