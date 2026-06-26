using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class ReconciliacaoStripeEstadoRepositoryTests(InfrastructureTestFixture fixture)
{
    private async Task LimparAsync()
    {
        await using var ctx = fixture.CreateContext();
        await ctx.ReconciliacoesStripeEstado.ExecuteDeleteAsync();
    }

    [Fact]
    public async Task ObterAsync_SemRegistro_RetornaNull()
    {
        await LimparAsync();

        await using var ctx = fixture.CreateContext();
        var resultado = await new ReconciliacaoStripeEstadoRepository(ctx).ObterAsync();

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task SalvarAsync_PersisteEObterAsyncRetorna()
    {
        await LimparAsync();
        var cursor = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
        var estado = ReconciliacaoStripeEstado.Criar(cursor, DateTime.UtcNow);

        await using (var ctx = fixture.CreateContext())
        {
            await new ReconciliacaoStripeEstadoRepository(ctx).SalvarAsync(estado);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var resultado = await new ReconciliacaoStripeEstadoRepository(ctx).ObterAsync();
            resultado.Should().NotBeNull();
            resultado!.Id.Should().Be(estado.Id);
            resultado.UltimoEventoReconciliadoUtc.Should().Be(cursor);
        }
    }

    [Fact]
    public async Task AvancarCursor_ViaTracking_PersisteAlteracoes()
    {
        await LimparAsync();
        var cursor = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
        var estado = ReconciliacaoStripeEstado.Criar(cursor, DateTime.UtcNow);

        await using (var ctx = fixture.CreateContext())
        {
            await new ReconciliacaoStripeEstadoRepository(ctx).SalvarAsync(estado);
            await ctx.SaveChangesAsync();
        }

        var novoCursor = new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc);
        await using (var ctx = fixture.CreateContext())
        {
            var repo = new ReconciliacaoStripeEstadoRepository(ctx);
            var atual = await repo.ObterAsync();
            atual!.AvancarCursor(novoCursor, DateTime.UtcNow);
            await repo.SalvarAsync(atual);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var resultado = await new ReconciliacaoStripeEstadoRepository(ctx).ObterAsync();
            resultado!.UltimoEventoReconciliadoUtc.Should().Be(novoCursor);
            resultado.UpdatedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task AvancarCursor_RetrocessoIgnorado()
    {
        var cursor = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
        var estado = ReconciliacaoStripeEstado.Criar(cursor, DateTime.UtcNow);

        estado.AvancarCursor(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);

        estado.UltimoEventoReconciliadoUtc.Should().Be(cursor);
        estado.UpdatedAt.Should().BeNull();
    }
}
