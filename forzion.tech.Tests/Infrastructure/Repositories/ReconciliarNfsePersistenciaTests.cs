using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Nfse.ReconciliarNfse;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class ReconciliarNfsePersistenciaTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTimeOffset Instante = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private static async Task<Guid> SeedTreinadorAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(conta.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private IServiceScopeFactory ScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => fixture.CreateContext());
        services.AddScoped<INotaFiscalRepository>(sp => new NotaFiscalRepository(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private ReconciliarNfseHandler Handler(AppDbContext ctx, IEmissorNfseService emissor) =>
        new(new NotaFiscalRepository(ctx), emissor, ScopeFactory(), new FakeTimeProvider(Instante),
            Mock.Of<ILogger<ReconciliarNfseHandler>>());

    [Fact]
    public async Task CancelamentoSolicitado_GovCancelada_PersisteCancelada()
    {
        Guid notaId;
        await using (var seed = fixture.CreateContext())
        {
            var treinadorId = await SeedTreinadorAsync(seed);
            var nota = NotaFiscal.CriarComissao(treinadorId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 50m, DateTime.UtcNow).Value;
            nota.MarcarEmitida("CHV-CANCEL", "100", DateTime.UtcNow, null, DateTime.UtcNow);
            nota.SolicitarCancelamento(DateTime.UtcNow);
            await seed.NotasFiscais.AddAsync(nota);
            await seed.SaveChangesAsync();
            notaId = nota.Id;
        }

        var emissor = new Mock<IEmissorNfseService>();
        emissor.Setup(e => e.ConsultarAsync("CHV-CANCEL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NfseStatus(NfseSituacao.Cancelada, null, null, null, null, null));

        await using (var ctx = fixture.CreateContext())
        {
            var result = await Handler(ctx, emissor.Object).HandleAsync(new ReconciliarNfseCommand());
            result.Value.Atualizadas.Should().Be(1);
        }

        await using var verificacao = fixture.CreateContext();
        var persistida = await new NotaFiscalRepository(verificacao).ObterPorIdAsync(notaId);
        persistida!.Status.Should().Be(NotaFiscalStatus.Cancelada);
    }

    [Fact]
    public async Task PendenteComChave_GovAutorizada_PersisteEmitida()
    {
        Guid notaId;
        await using (var seed = fixture.CreateContext())
        {
            var treinadorId = await SeedTreinadorAsync(seed);
            var nota = NotaFiscal.CriarComissao(treinadorId, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), 50m, DateTime.UtcNow).Value;
            await seed.NotasFiscais.AddAsync(nota);
            await seed.SaveChangesAsync();
            await seed.Database.ExecuteSqlRawAsync(
                "UPDATE notas_fiscais SET chave_acesso = 'CHV-EMITE' WHERE id = {0}", nota.Id);
            notaId = nota.Id;
        }

        var emissor = new Mock<IEmissorNfseService>();
        emissor.Setup(e => e.ConsultarAsync("CHV-EMITE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NfseStatus(NfseSituacao.Autorizada, "200", Instante.UtcDateTime, "danfse-ref", null, null));

        await using (var ctx = fixture.CreateContext())
        {
            var result = await Handler(ctx, emissor.Object).HandleAsync(new ReconciliarNfseCommand());
            result.Value.Atualizadas.Should().Be(1);
        }

        await using var verificacao = fixture.CreateContext();
        var persistida = await new NotaFiscalRepository(verificacao).ObterPorIdAsync(notaId);
        persistida!.Status.Should().Be(NotaFiscalStatus.Emitida);
        persistida.NumeroNfse.Should().Be("200");
    }
}
