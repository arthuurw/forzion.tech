using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Outbox.Handlers;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Outbox;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class CancelarNfseEfeitoHandlerIntegrationTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTimeOffset Instante = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Agora = Instante.UtcDateTime;

    private static async Task<Guid> SeedTreinadorAsync(AppDbContext ctx)
    {
        var conta = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, Agora).Value;
        var treinador = Treinador.Criar(conta.Id, $"Tr{Guid.NewGuid():N}", Agora).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private static async Task<NotaFiscal> SeedNotaSolicitadaAsync(AppDbContext ctx, DateTime emissao)
    {
        var treinadorId = await SeedTreinadorAsync(ctx);
        var nota = NotaFiscal.CriarComissao(treinadorId, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), 50m, emissao).Value;
        nota.MarcarEmitida("CHV-INT-1", "10", emissao, null, emissao);
        nota.SolicitarCancelamento(emissao);
        await ctx.NotasFiscais.AddAsync(nota);
        await ctx.SaveChangesAsync();
        return nota;
    }

    private static CancelarNfseEfeitoHandler Handler(AppDbContext ctx, Mock<IEmissorNfseService> emissor) =>
        new(
            new NotaFiscalRepository(ctx),
            emissor.Object,
            Options.Create(new NfseSettings { PrazoCancelamentoDias = 90 }),
            ctx,
            new FakeTimeProvider(Instante),
            Mock.Of<ILogger<CancelarNfseEfeitoHandler>>());

    private static string Payload(Guid notaId) =>
        JsonSerializer.Serialize(new CancelarNfsePayload(notaId, "Cancelamento por estorno do pagamento ao prestador."));

    [Fact]
    public async Task Sucesso_PersisteCancelada()
    {
        await using var seed = fixture.CreateContext();
        var nota = await SeedNotaSolicitadaAsync(seed, Agora);

        var emissor = new Mock<IEmissorNfseService>();
        emissor.Setup(e => e.CancelarAsync("CHV-INT-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NfseResultado(true, "CHV-INT-1", null, Agora, null, null, null));

        await using (var ctx = fixture.CreateContext())
            await Handler(ctx, emissor).ExecutarAsync(Payload(nota.Id));

        await using var verify = fixture.CreateContext();
        var persistida = await new NotaFiscalRepository(verify).ObterPorIdAsync(nota.Id);
        persistida!.Status.Should().Be(NotaFiscalStatus.Cancelada);
    }

    [Fact]
    public async Task Rejeicao_PersisteCancelamentoExpirado()
    {
        await using var seed = fixture.CreateContext();
        var nota = await SeedNotaSolicitadaAsync(seed, Agora);

        var emissor = new Mock<IEmissorNfseService>();
        emissor.Setup(e => e.CancelarAsync("CHV-INT-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NfseResultado(false, null, null, null, null, "E8001", "prazo expirado"));

        await using (var ctx = fixture.CreateContext())
            await Handler(ctx, emissor).ExecutarAsync(Payload(nota.Id));

        await using var verify = fixture.CreateContext();
        var persistida = await new NotaFiscalRepository(verify).ObterPorIdAsync(nota.Id);
        persistida!.Status.Should().Be(NotaFiscalStatus.CancelamentoExpirado);
    }
}
