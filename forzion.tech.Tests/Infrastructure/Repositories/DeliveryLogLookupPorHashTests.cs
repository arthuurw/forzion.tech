using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence.Repositories;
using forzion.tech.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class DeliveryLogLookupPorHashTests(InfrastructureTestFixture fixture)
{
    private static readonly IRecipientHasher Hasher =
        new RecipientHasher(Options.Create(new DeliveryLogSettings { RecipientHashKey = "test-key" }));

    [Fact]
    public async Task ListarPorEmail_CasaPorHashDoArg()
    {
        const string email = "lookup@test.com";
        await using (var ctx = fixture.CreateContext())
        {
            await ctx.EmailDeliveryLogs.ExecuteDeleteAsync();
            ctx.EmailDeliveryLogs.Add(EmailDeliveryLog.Criar(
                $"rid_{Guid.NewGuid():N}", "delivered", Hasher.Hash(email), DateTime.UtcNow, DateTime.UtcNow));
            await ctx.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext();
        var repo = new EmailDeliveryLogRepository(read, Hasher);

        (await repo.ListarPorEmailAsync(email)).Should().HaveCount(1);
        (await repo.ListarPorEmailAsync("outro@test.com")).Should().BeEmpty();
    }

    [Fact]
    public async Task AnonimizarPorEmail_ScrubaPorHash_ParaPlaceholder()
    {
        const string email = "erase@test.com";
        var rid = $"rid_{Guid.NewGuid():N}";
        await using (var ctx = fixture.CreateContext())
        {
            await ctx.EmailDeliveryLogs.ExecuteDeleteAsync();
            ctx.EmailDeliveryLogs.Add(EmailDeliveryLog.Criar(
                rid, "delivered", Hasher.Hash(email), DateTime.UtcNow, DateTime.UtcNow));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            await new EmailDeliveryLogRepository(ctx, Hasher).AnonimizarPorEmailAsync(email);
        }

        await using var verify = fixture.CreateContext();
        var log = await verify.EmailDeliveryLogs.AsNoTracking().FirstAsync(e => e.ResendMessageId == rid);
        log.RecipientEmailHash.Should().Be("(anonimizado)");
    }
}
