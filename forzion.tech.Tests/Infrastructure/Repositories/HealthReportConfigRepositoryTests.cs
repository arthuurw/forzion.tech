using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class HealthReportConfigRepositoryTests(InfrastructureTestFixture fixture)
{
    private static readonly TimeOnly Hora = new(7, 0);

    private async Task LimparAsync()
    {
        await using var ctx = fixture.CreateContext();
        await ctx.HealthReportConfigs.ExecuteDeleteAsync();
    }

    private static HealthReportConfig NovaConfig(bool ativo = true) =>
        HealthReportConfig.Criar(ativo, Hora, new[] { "admin@forzion.tech" }, true, true, true, true, DateTime.UtcNow).Value;

    [Fact]
    public async Task ObterAsync_SemRegistro_RetornaNull()
    {
        await LimparAsync();

        await using var ctx = fixture.CreateContext();
        var resultado = await new HealthReportConfigRepository(ctx).ObterAsync();

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task AdicionarAsync_PersisteEObterAsyncRetorna()
    {
        await LimparAsync();
        var config = NovaConfig();

        await using (var ctx = fixture.CreateContext())
        {
            await new HealthReportConfigRepository(ctx).AdicionarAsync(config);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var resultado = await new HealthReportConfigRepository(ctx).ObterAsync();
            resultado.Should().NotBeNull();
            resultado!.Id.Should().Be(config.Id);
            resultado.Destinatarios.Should().Be("admin@forzion.tech");
        }
    }

    [Fact]
    public async Task Atualizar_ViaTracking_PersisteAlteracoes()
    {
        await LimparAsync();
        var config = NovaConfig(ativo: false);

        await using (var ctx = fixture.CreateContext())
        {
            await new HealthReportConfigRepository(ctx).AdicionarAsync(config);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var atual = await new HealthReportConfigRepository(ctx).ObterAsync();
            atual!.Atualizar(true, new TimeOnly(9, 0), new[] { "ops@forzion.tech" }, false, false, false, false, DateTime.UtcNow);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var resultado = await new HealthReportConfigRepository(ctx).ObterAsync();
            resultado!.Ativo.Should().BeTrue();
            resultado.HoraEnvioUtc.Should().Be(new TimeOnly(9, 0));
            resultado.Destinatarios.Should().Be("ops@forzion.tech");
        }
    }
}
