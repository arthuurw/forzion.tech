using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Outbox;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class OutboxRepositoryLeaseTests(InfrastructureTestFixture fixture)
{
    [Fact]
    public async Task ObterProcessaveis_ItemTravado_NaoEntregueAOutroLeitor()
    {
        var agora = DateTime.UtcNow;
        var chave = $"fx:lease:{Guid.NewGuid():N}";

        await using (var seed = fixture.CreateContext())
        {
            seed.OutboxEfeitos.Add(OutboxEfeito.Criar("fx:lease", "{}", chave, agora.AddMinutes(-1)).Value);
            await seed.SaveChangesAsync();
        }

        await using var ctx1 = fixture.CreateContext();
        await using var ctx2 = fixture.CreateContext();
        var repo1 = new OutboxRepository(ctx1);
        var repo2 = new OutboxRepository(ctx2);

        // tx1 lê e SEGURA o lock do item (FOR UPDATE).
        await using var tx1 = await ctx1.Database.BeginTransactionAsync();
        var lote1 = await repo1.ObterProcessaveisAsync(100, agora);
        lote1.Should().Contain(e => e.ChaveIdempotencia == chave);

        // tx2 concorrente pula o item travado (SKIP LOCKED) — não o re-entrega.
        await using var tx2 = await ctx2.Database.BeginTransactionAsync();
        var lote2 = await repo2.ObterProcessaveisAsync(100, agora);
        lote2.Should().NotContain(e => e.ChaveIdempotencia == chave);

        await tx1.RollbackAsync();
        await tx2.RollbackAsync();
    }
}
