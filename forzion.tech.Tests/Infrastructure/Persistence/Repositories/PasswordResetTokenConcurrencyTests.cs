using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Persistence.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class PasswordResetTokenConcurrencyTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Base = new(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);

    private static PasswordResetToken Pendente(Guid contaId, string hash) =>
        PasswordResetToken.Criar(contaId, hash, Base.AddHours(1), Base).Value;

    [Fact]
    public async Task DoisTokensPendentesMesmaConta_RejeitadoPeloIndiceUnicoParcial()
    {
        var contaId = Guid.NewGuid();

        await using var ctx = fixture.CreateContext();
        ctx.PasswordResetTokens.Add(Pendente(contaId, Guid.NewGuid().ToString("N")));
        ctx.PasswordResetTokens.Add(Pendente(contaId, Guid.NewGuid().ToString("N")));

        var act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task NovoPendenteAposInvalidarAnterior_Aceito()
    {
        var contaId = Guid.NewGuid();
        var primeiroHash = Guid.NewGuid().ToString("N");

        await using (var seed = fixture.CreateContext())
        {
            seed.PasswordResetTokens.Add(Pendente(contaId, primeiroHash));
            await seed.SaveChangesAsync();
        }

        await using var ctx = fixture.CreateContext();
        var repo = new PasswordResetTokenRepository(ctx);
        await repo.InvalidarPendentesPorContaAsync(contaId, Base.AddMinutes(5));
        await repo.AdicionarAsync(Pendente(contaId, Guid.NewGuid().ToString("N")));

        var act = async () => await ctx.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PendentesDeContasDistintas_Coexistem()
    {
        await using var ctx = fixture.CreateContext();
        ctx.PasswordResetTokens.Add(Pendente(Guid.NewGuid(), Guid.NewGuid().ToString("N")));
        ctx.PasswordResetTokens.Add(Pendente(Guid.NewGuid(), Guid.NewGuid().ToString("N")));

        var act = async () => await ctx.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }
}
