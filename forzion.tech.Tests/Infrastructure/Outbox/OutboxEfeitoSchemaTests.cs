using System.Text.Json;
using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Outbox;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class OutboxEfeitoSchemaTests(InfrastructureTestFixture fixture)
{
    [Fact]
    public async Task Persistir_RoundTrip_PreservaCampos()
    {
        var agora = DateTime.UtcNow;
        var chave = $"fx:teste:{Guid.NewGuid():N}";
        var efeito = OutboxEfeito.Criar("fx:teste", "{\"x\":1}", chave, agora).Value;

        await using (var ctx = fixture.CreateContext())
        {
            ctx.OutboxEfeitos.Add(efeito);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var lido = await ctx.OutboxEfeitos.SingleAsync(e => e.ChaveIdempotencia == chave);
            lido.Tipo.Should().Be("fx:teste");
            // jsonb normaliza whitespace; comparo o conteúdo parseado, não a string crua.
            JsonDocument.Parse(lido.Payload).RootElement.GetProperty("x").GetInt32().Should().Be(1);
            lido.Status.Should().Be(OutboxStatus.Pendente);
            lido.Tentativas.Should().Be(0);
            lido.ProcessadoEm.Should().BeNull();
        }
    }

    [Fact]
    public async Task ChaveIdempotencia_Duplicada_VioladaPeloIndiceUnico()
    {
        var agora = DateTime.UtcNow;
        var chave = $"fx:dup:{Guid.NewGuid():N}";

        await using var ctx = fixture.CreateContext();
        ctx.OutboxEfeitos.Add(OutboxEfeito.Criar("fx:dup", "{}", chave, agora).Value);
        await ctx.SaveChangesAsync();

        ctx.OutboxEfeitos.Add(OutboxEfeito.Criar("fx:dup", "{}", chave, agora).Value);
        var act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
