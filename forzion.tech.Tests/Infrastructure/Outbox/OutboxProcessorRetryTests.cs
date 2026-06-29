using System.Text.Json;
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
using Npgsql;

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

    private sealed class FxErroPermanente : IOutboxEfeitoHandler
    {
        public string Tipo => "fx:teste";
        public Task ExecutarAsync(string payload, CancellationToken cancellationToken = default) =>
            throw new JsonException("payload corrompido");
    }

    private sealed class FxOcioso(TimeSpan espera) : IOutboxEfeitoHandler
    {
        public string Tipo => "fx:teste";
        public Task ExecutarAsync(string payload, CancellationToken cancellationToken = default) =>
            Task.Delay(espera, cancellationToken);
    }

    // Simula shutdown: cancela o token durante o dispatch e lança OCE observando-o.
    private sealed class FxCancelaEThrowOce(CancellationTokenSource cts) : IOutboxEfeitoHandler
    {
        public string Tipo => "fx:teste";
        public Task ExecutarAsync(string payload, CancellationToken cancellationToken = default)
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }
    }

    private static OutboxProcessor CriarProcessor(AppDbContext ctx, IOutboxEfeitoHandler handler, int maxTentativas, TimeSpan? backoffBase = null, TimeSpan? timeoutTransacaoIdle = null)
    {
        var dispatcher = new OutboxDispatcher(new NoOpDispatcher(), new OutboxDurabilityRegistry(), [handler]);
        // BackoffBase=Zero: proxima_tentativa = agora, então cada ciclo seguinte re-elege o item.
        var options = Options.Create(new OutboxOptions
        {
            MaxTentativas = maxTentativas,
            BackoffBase = backoffBase ?? TimeSpan.Zero,
            LotePorCiclo = 50,
            TimeoutTransacaoIdle = timeoutTransacaoIdle ?? TimeSpan.FromSeconds(60),
        });
        return new OutboxProcessor(ctx, new OutboxRepository(ctx), dispatcher, TimeProvider.System, options, NullLogger<OutboxProcessor>.Instance);
    }

    private async Task<string> SemearEfeitoAsync()
    {
        var chave = $"fx:teste:{Guid.NewGuid():N}";
        await using var seed = fixture.CreateContext();
        // Container é compartilhado pela collection e ProcessarLoteAsync elege TODOS os itens
        // Pendente (não filtra por chave). Sem zerar, um item Pendente deixado por outro teste
        // (ex.: o de shutdown) seria processado pelo MESMO handler aqui, dobrando Chamadas e
        // corrompendo a contagem de tentativas. Isola cada teste com tabela limpa.
        await seed.OutboxEfeitos.ExecuteDeleteAsync();
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

    [Fact]
    public async Task ProcessarLote_ErroPermanente_VaiParaFalhouNaPrimeiraTentativa()
    {
        var chave = await SemearEfeitoAsync();
        await using var ctx = fixture.CreateContext();
        var processor = CriarProcessor(ctx, new FxErroPermanente(), maxTentativas: 5);

        await processor.ProcessarLoteAsync();

        await using var verify = fixture.CreateContext();
        var efeito = await verify.OutboxEfeitos.AsNoTracking().SingleAsync(o => o.ChaveIdempotencia == chave);
        efeito.Status.Should().Be(OutboxStatus.Falhou);
        efeito.Tentativas.Should().Be(1);
        efeito.UltimoErro.Should().Contain("corrompido");
    }

    [Fact]
    public async Task ProcessarLote_ErroTransiente_PermanecePendenteComProximaTentativaFutura()
    {
        var chave = await SemearEfeitoAsync();
        var antes = DateTime.UtcNow;
        await using var ctx = fixture.CreateContext();
        var processor = CriarProcessor(ctx, new FxFalhaNVezes(falhas: 1), maxTentativas: 5, backoffBase: TimeSpan.FromMinutes(1));

        await processor.ProcessarLoteAsync();

        await using var verify = fixture.CreateContext();
        var efeito = await verify.OutboxEfeitos.AsNoTracking().SingleAsync(o => o.ChaveIdempotencia == chave);
        efeito.Status.Should().Be(OutboxStatus.Pendente);
        efeito.Tentativas.Should().Be(1);
        efeito.ProximaTentativa.Should().BeAfter(antes);
    }

    [Fact]
    public async Task ProcessarLote_TransacaoOciosaAlemDoTimeout_LiberaLeaseSemConcluir()
    {
        var chave = await SemearEfeitoAsync();
        await using var ctx = fixture.CreateContext();
        var processor = CriarProcessor(ctx, new FxOcioso(TimeSpan.FromSeconds(2)), maxTentativas: 5, timeoutTransacaoIdle: TimeSpan.FromMilliseconds(200));

        var act = async () => await processor.ProcessarLoteAsync();
        await act.Should().ThrowAsync<NpgsqlException>();

        await using var verify = fixture.CreateContext();
        var efeito = await verify.OutboxEfeitos.AsNoTracking().SingleAsync(o => o.ChaveIdempotencia == chave);
        efeito.Status.Should().Be(OutboxStatus.Pendente, "sessão reaped aborta a transação; o lease volta intacto");
        efeito.Tentativas.Should().Be(0);
    }

    // cancelamento de shutdown re-lança (rollback do lease) em vez de queimar uma
    // tentativa — o item volta Pendente, intacto, para o próximo boot.
    [Fact]
    public async Task ProcessarLote_CancelamentoDeShutdown_RelancaSemContarTentativa()
    {
        var chave = await SemearEfeitoAsync();
        using var cts = new CancellationTokenSource();
        await using var ctx = fixture.CreateContext();
        var processor = CriarProcessor(ctx, new FxCancelaEThrowOce(cts), maxTentativas: 5);

        var act = async () => await processor.ProcessarLoteAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        await using var verify = fixture.CreateContext();
        var efeito = await verify.OutboxEfeitos.AsNoTracking().SingleAsync(o => o.ChaveIdempotencia == chave);
        efeito.Status.Should().Be(OutboxStatus.Pendente);
        efeito.Tentativas.Should().Be(0, "OCE de shutdown não é falha de efeito");
    }
}
