using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure;

// Regressão: um domain event handler que chama CommitAsync de novo (re-entrância)
// não pode fazer os eventos da transação original serem re-despachados. Antes do
// fix isso duplicava a projeção Assinante no cadastro de aluno (duplicate key → 500).
[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class AppDbContextDomainEventReentrancyTests(InfrastructureTestFixture fixture)
{
    private AppDbContext CreateContext(IDomainEventDispatcher dispatcher)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, schema: "homolog", dispatcher);
    }

    [Fact]
    public async Task CommitAsync_HandlerReentrante_DespachaCadaEventoUmaVez()
    {
        var dispatcher = new DispatcherReentrante();
        await using var ctx = CreateContext(dispatcher);
        await ctx.Database.EnsureCreatedAsync();
        dispatcher.Context = ctx;

        var conta = Conta.Criar(Email.Criar($"reentr{Guid.NewGuid():N}@test.com"), "hash", TipoConta.Aluno, DateTime.UtcNow);
        var aluno = Aluno.Criar(conta.Id, "Reentrancia", DateTime.UtcNow);
        var eventosEsperados = aluno.DomainEvents.Count;
        eventosEsperados.Should().BeGreaterThan(0, "Aluno.Criar precisa levantar ao menos um evento para o cenário ser válido");

        await ctx.Contas.AddAsync(conta);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.CommitAsync();

        dispatcher.TotalDespachado.Should().Be(eventosEsperados,
            "cada evento deve ser despachado exatamente uma vez, mesmo com um handler chamando CommitAsync de novo");
    }

    private sealed class DispatcherReentrante : IDomainEventDispatcher
    {
        public AppDbContext? Context { get; set; }
        public int TotalDespachado { get; private set; }
        private bool _reentrou;

        public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            TotalDespachado += events.Count;

            // Simula um handler que persiste algo e re-comita (como a projeção billing).
            if (!_reentrou && Context is not null)
            {
                _reentrou = true;
                await Context.CommitAsync(cancellationToken);
            }
        }
    }
}
