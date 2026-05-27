using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class ErrorLogRepositoryTests(InfrastructureTestFixture fixture)
{
    private async Task LimparAsync()
    {
        await using var ctx = fixture.CreateContext();
        await ctx.ErrorLogs.ExecuteDeleteAsync();
    }

    [Fact]
    public async Task AdicionarAsync_Persiste()
    {
        await LimparAsync();
        var entry = ErrorLogEntry.Criar(DateTime.UtcNow, "Error", "Worker", "boom");

        await using (var ctx = fixture.CreateContext())
        {
            await new ErrorLogRepository(ctx).AdicionarAsync(entry);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            (await ctx.ErrorLogs.AnyAsync(e => e.Id == entry.Id)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ContarDesdeAsync_ContaApenasDentroDaJanela()
    {
        await LimparAsync();
        var agora = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var desde = agora.AddHours(-24);

        await using (var ctx = fixture.CreateContext())
        {
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-1), "Error", "A", "dentro"));
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-12), "Error", "B", "dentro"));
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-48), "Error", "C", "fora"));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var total = await new ErrorLogRepository(ctx).ContarDesdeAsync(desde);
            total.Should().Be(2);
        }
    }

    [Fact]
    public async Task ListarDesdeAsync_FiltraOrdenaDescERespeitaLimite()
    {
        await LimparAsync();
        var agora = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var desde = agora.AddHours(-24);

        await using (var ctx = fixture.CreateContext())
        {
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-2), "Error", "A", "msg"));
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-1), "Error", "B", "msg"));
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-3), "Error", "C", "msg"));
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-48), "Error", "D", "fora"));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var resultado = await new ErrorLogRepository(ctx).ListarDesdeAsync(desde, 2);

            resultado.Should().HaveCount(2);
            resultado.Select(e => e.OcorridoEm).Should().BeInDescendingOrder();
            resultado[0].OcorridoEm.Should().Be(agora.AddHours(-1));
        }
    }
}
