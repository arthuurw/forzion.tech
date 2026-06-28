using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

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
        var entry = ErrorLogEntry.Criar(DateTime.UtcNow, "Error", "Worker", "boom", DateTime.UtcNow).Value;

        await using (var ctx = fixture.CreateContext())
        {
            await new ErrorLogRepository(ctx, TimeProvider.System).AdicionarAsync(entry);
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
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-1), "Error", "A", "dentro", agora).Value);
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-12), "Error", "B", "dentro", agora).Value);
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-48), "Error", "C", "fora", agora).Value);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var total = await new ErrorLogRepository(ctx, TimeProvider.System).ContarDesdeAsync(desde);
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
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-2), "Error", "A", "msg", agora).Value);
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-1), "Error", "B", "msg", agora).Value);
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-3), "Error", "C", "msg", agora).Value);
            await ctx.ErrorLogs.AddAsync(ErrorLogEntry.Criar(agora.AddHours(-48), "Error", "D", "fora", agora).Value);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var resultado = await new ErrorLogRepository(ctx, TimeProvider.System).ListarDesdeAsync(desde, 2);

            resultado.Should().HaveCount(2);
            resultado.Select(e => e.OcorridoEm).Should().BeInDescendingOrder();
            resultado[0].OcorridoEm.Should().Be(agora.AddHours(-1));
        }
    }

    [Fact]
    public async Task LimparAntigosAsync_RemoveAlemDe90d_PreservaRecentes()
    {
        await LimparAsync();
        var agora = new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var clock = new FakeTimeProvider(new DateTimeOffset(agora, TimeSpan.Zero));
        var antigo = ErrorLogEntry.Criar(agora.AddDays(-91), "Error", "A", "antigo", agora.AddDays(-91)).Value;
        var recente = ErrorLogEntry.Criar(agora.AddDays(-1), "Error", "B", "recente", agora.AddDays(-1)).Value;

        await using (var ctx = fixture.CreateContext())
        {
            await ctx.ErrorLogs.AddRangeAsync(antigo, recente);
            await ctx.SaveChangesAsync();
        }

        int removidos;
        await using (var ctx = fixture.CreateContext())
        {
            removidos = await new ErrorLogRepository(ctx, clock).LimparAntigosAsync();
        }

        removidos.Should().Be(1);
        await using (var ctx = fixture.CreateContext())
        {
            (await ctx.ErrorLogs.AnyAsync(e => e.Id == antigo.Id)).Should().BeFalse();
            (await ctx.ErrorLogs.AnyAsync(e => e.Id == recente.Id)).Should().BeTrue();
        }
    }
}
