using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Persistence.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class RefreshTokenRepositoryTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Base = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BuscarPorHashAsync_RoundTrip_RecuperaToken()
    {
        var familia = RefreshTokenFamily.Criar(Guid.NewGuid(), Base.AddDays(90), Base).Value;
        var hash = Guid.NewGuid().ToString("N");
        var token = RefreshToken.Criar(familia.Id, hash, Base.AddDays(7), Base).Value;

        await using (var seed = fixture.CreateContext())
        {
            seed.RefreshTokenFamilies.Add(familia);
            seed.RefreshTokens.Add(token);
            await seed.SaveChangesAsync();
        }

        await using var ctx = fixture.CreateContext();
        var repo = new RefreshTokenRepository(ctx);

        var encontrado = await repo.BuscarPorHashAsync(hash);

        encontrado.Should().NotBeNull();
        encontrado!.FamiliaId.Should().Be(familia.Id);
    }

    [Fact]
    public async Task TokenHash_Unico_RejeitaDuplicado()
    {
        var familia = RefreshTokenFamily.Criar(Guid.NewGuid(), Base.AddDays(90), Base).Value;
        var hash = Guid.NewGuid().ToString("N");

        await using var ctx = fixture.CreateContext();
        ctx.RefreshTokenFamilies.Add(familia);
        ctx.RefreshTokens.Add(RefreshToken.Criar(familia.Id, hash, Base.AddDays(7), Base).Value);
        ctx.RefreshTokens.Add(RefreshToken.Criar(familia.Id, hash, Base.AddDays(7), Base).Value);

        var act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DeletarFamilia_CascateiaTokens()
    {
        var familia = RefreshTokenFamily.Criar(Guid.NewGuid(), Base.AddDays(90), Base).Value;
        var hash = Guid.NewGuid().ToString("N");
        var token = RefreshToken.Criar(familia.Id, hash, Base.AddDays(7), Base).Value;

        await using (var seed = fixture.CreateContext())
        {
            seed.RefreshTokenFamilies.Add(familia);
            seed.RefreshTokens.Add(token);
            await seed.SaveChangesAsync();
        }

        await using (var del = fixture.CreateContext())
        {
            await del.RefreshTokenFamilies.Where(f => f.Id == familia.Id).ExecuteDeleteAsync();
        }

        await using var verify = fixture.CreateContext();
        (await verify.RefreshTokens.AnyAsync(t => t.Id == token.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ListarAtivasPorConta_FiltraRevogadasEAbsoluto()
    {
        var contaId = Guid.NewGuid();
        var ativa = RefreshTokenFamily.Criar(contaId, Base.AddDays(90), Base).Value;
        var revogada = RefreshTokenFamily.Criar(contaId, Base.AddDays(90), Base).Value;
        revogada.Revogar(MotivoRevogacaoFamilia.Logout, Base);
        var expirada = RefreshTokenFamily.Criar(contaId, Base.AddDays(1), Base).Value;

        await using (var seed = fixture.CreateContext())
        {
            seed.RefreshTokenFamilies.AddRange(ativa, revogada, expirada);
            await seed.SaveChangesAsync();
        }

        await using var ctx = fixture.CreateContext();
        var repo = new RefreshTokenFamilyRepository(ctx);

        var ativas = await repo.ListarAtivasPorContaAsync(contaId, Base.AddDays(2));

        ativas.Select(f => f.Id).Should().Contain(ativa.Id);
        ativas.Select(f => f.Id).Should().NotContain(revogada.Id);
        ativas.Select(f => f.Id).Should().NotContain(expirada.Id);
    }

    [Fact]
    public async Task LimparExpiradasAsync_RemoveRevogadasEAposAbsoluto()
    {
        var contaId = Guid.NewGuid();
        var ativa = RefreshTokenFamily.Criar(contaId, Base.AddDays(90), Base).Value;
        var revogada = RefreshTokenFamily.Criar(contaId, Base.AddDays(90), Base).Value;
        revogada.Revogar(MotivoRevogacaoFamilia.TrocaSenha, Base);
        var expirada = RefreshTokenFamily.Criar(contaId, Base.AddDays(1), Base).Value;

        await using (var seed = fixture.CreateContext())
        {
            seed.RefreshTokenFamilies.AddRange(ativa, revogada, expirada);
            await seed.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var repo = new RefreshTokenFamilyRepository(ctx);
            await repo.LimparExpiradasAsync(Base.AddDays(2));
        }

        await using var verify = fixture.CreateContext();
        (await verify.RefreshTokenFamilies.AnyAsync(f => f.Id == ativa.Id)).Should().BeTrue();
        (await verify.RefreshTokenFamilies.AnyAsync(f => f.Id == revogada.Id)).Should().BeFalse();
        (await verify.RefreshTokenFamilies.AnyAsync(f => f.Id == expirada.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ExcluirPorContaIdAsync_PurgaFamiliasETokensDoTitular()
    {
        var contaId = Guid.NewGuid();
        var outraConta = Guid.NewGuid();
        var familia = RefreshTokenFamily.Criar(contaId, Base.AddDays(90), Base).Value;
        var token = RefreshToken.Criar(familia.Id, Guid.NewGuid().ToString("N"), Base.AddDays(7), Base).Value;
        var familiaAlheia = RefreshTokenFamily.Criar(outraConta, Base.AddDays(90), Base).Value;

        await using (var seed = fixture.CreateContext())
        {
            seed.RefreshTokenFamilies.AddRange(familia, familiaAlheia);
            seed.RefreshTokens.Add(token);
            await seed.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var repo = new RefreshTokenFamilyRepository(ctx);
            await repo.ExcluirPorContaIdAsync(contaId);
        }

        await using var verify = fixture.CreateContext();
        (await verify.RefreshTokenFamilies.AnyAsync(f => f.ContaId == contaId)).Should().BeFalse();
        // cascade apaga os tokens da família purgada
        (await verify.RefreshTokens.AnyAsync(t => t.Id == token.Id)).Should().BeFalse();
        // família de outra conta intocada
        (await verify.RefreshTokenFamilies.AnyAsync(f => f.Id == familiaAlheia.Id)).Should().BeTrue();
    }
}
