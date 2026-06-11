using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Infrastructure.Persistence.Repositories;

// Prova que o filtro de expiração usa o clock injetado (TimeProvider), não o relógio real:
// avançar o FakeTimeProvider além de ExpiraEm muda o resultado de EstaRevogado/LimparExpirados.
[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class TokenRevogadoRepositoryTests(InfrastructureTestFixture fixture)
{
    [Fact]
    public async Task EstaRevogadoAsync_ExpiraNoClockInjetado()
    {
        var baseTime = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var time = new FakeTimeProvider(new DateTimeOffset(baseTime));
        var jti = Guid.NewGuid();

        await using (var seed = fixture.CreateContext())
        {
            seed.TokensRevogados.Add(TokenRevogado.Criar(jti, baseTime.AddHours(1), baseTime).Value);
            await seed.SaveChangesAsync();
        }

        await using var ctx = fixture.CreateContext();
        var repo = new TokenRevogadoRepository(ctx, time);

        (await repo.EstaRevogadoAsync(jti)).Should().BeTrue();

        time.SetUtcNow(new DateTimeOffset(baseTime.AddHours(2)));
        (await repo.EstaRevogadoAsync(jti)).Should().BeFalse();
    }

    [Fact]
    public async Task LimparExpiradosAsync_RemoveApenasOsExpiradosNoClockInjetado()
    {
        var baseTime = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var time = new FakeTimeProvider(new DateTimeOffset(baseTime.AddHours(3)));
        var jtiExpirado = Guid.NewGuid();
        var jtiValido = Guid.NewGuid();

        await using (var seed = fixture.CreateContext())
        {
            seed.TokensRevogados.Add(TokenRevogado.Criar(jtiExpirado, baseTime.AddHours(1), baseTime).Value);
            seed.TokensRevogados.Add(TokenRevogado.Criar(jtiValido, baseTime.AddHours(5), baseTime).Value);
            await seed.SaveChangesAsync();
        }

        await using var ctx = fixture.CreateContext();
        var repo = new TokenRevogadoRepository(ctx, time);

        await repo.LimparExpiradosAsync();

        // Delete é global; asserta só os jtis deste teste (clock=15h: expira-13h removido, expira-17h mantido).
        await using var verify = fixture.CreateContext();
        (await verify.TokensRevogados.AnyAsync(t => t.Jti == jtiExpirado)).Should().BeFalse();
        (await verify.TokensRevogados.AnyAsync(t => t.Jti == jtiValido)).Should().BeTrue();
    }
}
