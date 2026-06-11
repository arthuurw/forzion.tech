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

namespace forzion.tech.Tests.Infrastructure.Outbox;

// Política de retry do worker: falha transiente é re-tentada até concluir; falha
// permanente vira estado terminal Falhou após MaxTentativas.
[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class OutboxProcessorRetryTests(InfrastructureTestFixture fixture)
{
    private sealed class NoOpDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DispatchDuravelAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FxFalhaNVezes(int falhas) : IOutboxEfeitoHandler
    {
        public string Tipo => "fx:teste";
        public int Chamadas { get; private set; }
        public Task ExecutarAsync(string payload, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            if (Chamadas <= falhas)
                throw new InvalidOperationException("transiente");
            return Task.CompletedTask;
        }
    }

    private sealed class FxSempreFalha : IOutboxEfeitoHandler
    {
        public string Tipo => "fx:teste";
        public Task ExecutarAsync(string payload, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("permanente");
    }

    private static OutboxProcessor CriarProcessor(AppDbContext ctx, IOutboxEfeitoHandler handler, int maxTentativas)
    {
        var dispatcher = new OutboxDispatcher(new NoOpDispatcher(), new OutboxDurabilityRegistry(), [handler]);
        // BackoffBase=Zero: proxima_tentativa = agora, então cada ciclo seguinte re-elege o item.
        var options = Options.Create(new OutboxOptions
        {
            MaxTentativas = maxTentativas,
            BackoffBase = TimeSpan.Zero,
            LotePorCiclo = 50,
        });
        return new OutboxProcessor(ctx, new OutboxRepository(ctx), dispatcher, TimeProvider.System, options, NullLogger<OutboxProcessor>.Instance);
    }

    private async Task<string> SemearEfeitoAsync()
    {
        var chave = $"fx:teste:{Guid.NewGuid():N}";
        await using var seed = fixture.CreateContext();
        seed.OutboxEfeitos.Add(OutboxEfeito.Criar("fx:teste", "{}", chave, DateTime.UtcNow.AddMinutes(-1)).Value);
        await seed.SaveChangesAsync();
        return chave;
    }

    [Fact]
    public async Task ProcessarLote_FalhaTransiente_RetentaAteConcluir()
    {
        var chave = await SemearEfeitoAsync();
        await using var ctx = fixture.CreateContext();
        var processor = CriarProcessor(ctx, new FxFalhaNVezes(falhas: 2), maxTentativas: 5);

        for (var ciclo = 0; ciclo < 3; ciclo++)
            await processor.ProcessarLoteAsync();

        await using var verify = fixture.CreateContext();
        var efeito = await verify.OutboxEfeitos.AsNoTracking().SingleAsync(o => o.ChaveIdempotencia == chave);
        efeito.Status.Should().Be(OutboxStatus.Concluido);
        efeito.Tentativas.Should().Be(2, "duas falhas antes do sucesso");
        efeito.ProcessadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessarLote_FalhaPermanente_VaiParaFalhouAposMaxTentativas()
    {
        var chave = await SemearEfeitoAsync();
        await using var ctx = fixture.CreateContext();
        var processor = CriarProcessor(ctx, new FxSempreFalha(), maxTentativas: 3);

        for (var ciclo = 0; ciclo < 3; ciclo++)
            await processor.ProcessarLoteAsync();

        await using var verify = fixture.CreateContext();
        var efeito = await verify.OutboxEfeitos.AsNoTracking().SingleAsync(o => o.ChaveIdempotencia == chave);
        efeito.Status.Should().Be(OutboxStatus.Falhou);
        efeito.Tentativas.Should().Be(3);
        efeito.UltimoErro.Should().Contain("permanente");
    }
}
