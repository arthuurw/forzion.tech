using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class HealthSnapshotRepositoryTests(InfrastructureTestFixture fixture)
{
    private async Task LimparAsync()
    {
        await using var ctx = fixture.CreateContext();
        await ctx.HealthSnapshots.ExecuteDeleteAsync();
    }

    [Fact]
    public async Task AdicionarAsync_Persiste()
    {
        await LimparAsync();
        var snapshot = HealthSnapshot.Criar("homolog", StatusSaude.Ok, "{}", DateTime.UtcNow).Value;

        await using (var ctx = fixture.CreateContext())
        {
            await new HealthSnapshotRepository(ctx).AdicionarAsync(snapshot);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            (await ctx.HealthSnapshots.AnyAsync(s => s.Id == snapshot.Id)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ListarRecentesAsync_OrdenaDescERespeitaLimite()
    {
        await LimparAsync();
        var baseTime = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

        await using (var ctx = fixture.CreateContext())
        {
            for (var i = 0; i < 5; i++)
            {
                var s = HealthSnapshot.Criar("homolog", StatusSaude.Ok, "{}", baseTime.AddMinutes(i)).Value;
                await ctx.HealthSnapshots.AddAsync(s);
            }
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var resultado = await new HealthSnapshotRepository(ctx).ListarRecentesAsync(3);

            resultado.Should().HaveCount(3);
            resultado.Select(s => s.CapturadoEm).Should().BeInDescendingOrder();
            resultado[0].CapturadoEm.Should().Be(baseTime.AddMinutes(4));
        }
    }
}
