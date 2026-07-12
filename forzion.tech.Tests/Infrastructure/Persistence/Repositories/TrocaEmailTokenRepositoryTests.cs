using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Infrastructure.Persistence.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class TrocaEmailTokenRepositoryTests(InfrastructureTestFixture fixture)
{
    [Fact]
    public async Task LimparExpiradosAsync_RemoveExpiradosEUsados_MantemPendentesValidos()
    {
        var baseTime = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var time = new FakeTimeProvider(new DateTimeOffset(baseTime.AddHours(3)));
        var contaId = Guid.NewGuid();

        var expirado = TrocaEmailToken.Criar(contaId, "expirado@test.com", Guid.NewGuid().ToString(), baseTime.AddHours(1), baseTime).Value;
        var usado = TrocaEmailToken.Criar(contaId, "usado@test.com", Guid.NewGuid().ToString(), baseTime.AddHours(5), baseTime).Value;
        usado.MarcarComoUsado(baseTime.AddHours(2));
        var pendenteValido = TrocaEmailToken.Criar(contaId, "pendente@test.com", Guid.NewGuid().ToString(), baseTime.AddHours(5), baseTime).Value;

        await using (var seed = fixture.CreateContext())
        {
            seed.TrocaEmailTokens.AddRange(expirado, usado, pendenteValido);
            await seed.SaveChangesAsync();
        }

        await using var ctx = fixture.CreateContext();
        var repo = new TrocaEmailTokenRepository(ctx, time);

        await repo.LimparExpiradosAsync();

        await using var verify = fixture.CreateContext();
        (await verify.TrocaEmailTokens.AnyAsync(t => t.Id == expirado.Id)).Should().BeFalse();
        (await verify.TrocaEmailTokens.AnyAsync(t => t.Id == usado.Id)).Should().BeFalse();
        (await verify.TrocaEmailTokens.AnyAsync(t => t.Id == pendenteValido.Id)).Should().BeTrue();
    }
}
