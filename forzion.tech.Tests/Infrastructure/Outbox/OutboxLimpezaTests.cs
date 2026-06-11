using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Outbox;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using forzion.tech.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Infrastructure.Outbox;

// Limpeza de retenção: itens Concluido além da janela são removidos; Concluido recente,
// Pendente e Falhou são preservados.
[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class OutboxLimpezaTests(InfrastructureTestFixture fixture)
{
    private sealed class NoOpDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DispatchDuravelAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static OutboxEfeito Concluido(string chave, DateTime criadoEm, DateTime processadoEm)
    {
        var efeito = OutboxEfeito.Criar("fx:teste", "{}", chave, criadoEm).Value;
        efeito.MarcarProcessando();
        efeito.MarcarConcluido(processadoEm);
        return efeito;
    }

    [Fact]
    public async Task LimparConcluidos_RemoveAposRetencao_PreservaRecenteEPendente()
    {
        var agora = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var time = new FakeTimeProvider(new DateTimeOffset(agora));

        var chaveVelho = $"velho:{Guid.NewGuid():N}";
        var chaveRecente = $"recente:{Guid.NewGuid():N}";
        var chavePendente = $"pendente:{Guid.NewGuid():N}";

        await using (var seed = fixture.CreateContext())
        {
            // Processado há 8 dias: além da retenção padrão de 7 → removido.
            seed.OutboxEfeitos.Add(Concluido(chaveVelho, agora.AddDays(-9), agora.AddDays(-8)));
            // Processado ontem: dentro da janela → preservado.
            seed.OutboxEfeitos.Add(Concluido(chaveRecente, agora.AddDays(-2), agora.AddDays(-1)));
            // Nunca processado → preservado independentemente da idade.
            seed.OutboxEfeitos.Add(OutboxEfeito.Criar("fx:teste", "{}", chavePendente, agora.AddDays(-30)).Value);
            await seed.SaveChangesAsync();
        }

        await using var ctx = fixture.CreateContext();
        var options = Options.Create(new OutboxOptions { RetencaoConcluidos = TimeSpan.FromDays(7) });
        var processor = new OutboxProcessor(
            ctx,
            new OutboxRepository(ctx),
            new OutboxDispatcher(new NoOpDispatcher(), new OutboxDurabilityRegistry(), []),
            time,
            options,
            NullLogger<OutboxProcessor>.Instance);

        var removidos = await processor.LimparConcluidosAsync();

        removidos.Should().Be(1);
        await using var verify = fixture.CreateContext();
        var restantes = await verify.OutboxEfeitos.AsNoTracking()
            .Where(o => o.ChaveIdempotencia == chaveVelho || o.ChaveIdempotencia == chaveRecente || o.ChaveIdempotencia == chavePendente)
            .Select(o => o.ChaveIdempotencia)
            .ToListAsync();
        restantes.Should().BeEquivalentTo([chaveRecente, chavePendente]);
    }
}
