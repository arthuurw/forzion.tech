using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence.Repositories;
using forzion.tech.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

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
    public async Task MarcarUsadoSeNaoUsadoAsync_TokenNaoUsado_Afeta1LinhaECarimba()
    {
        var familia = RefreshTokenFamily.Criar(Guid.NewGuid(), Base.AddDays(90), Base).Value;
        var token = RefreshToken.Criar(familia.Id, Guid.NewGuid().ToString("N"), Base.AddDays(7), Base).Value;
        var sucessorId = Guid.NewGuid();

        await using (var seed = fixture.CreateContext())
        {
            seed.RefreshTokenFamilies.Add(familia);
            seed.RefreshTokens.Add(token);
            await seed.SaveChangesAsync();
        }

        int afetadas;
        await using (var ctx = fixture.CreateContext())
        {
            afetadas = await new RefreshTokenRepository(ctx).MarcarUsadoSeNaoUsadoAsync(token.Id, Base.AddMinutes(10), sucessorId);
        }

        afetadas.Should().Be(1);
        await using var verify = fixture.CreateContext();
        var persistido = await verify.RefreshTokens.FirstAsync(t => t.Id == token.Id);
        persistido.UsadoEm.Should().Be(Base.AddMinutes(10));
        persistido.SubstituidoPorId.Should().Be(sucessorId);
    }

    [Fact]
    public async Task MarcarUsadoSeNaoUsadoAsync_TokenJaUsado_Afeta0Linhas()
    {
        var familia = RefreshTokenFamily.Criar(Guid.NewGuid(), Base.AddDays(90), Base).Value;
        var token = RefreshToken.Criar(familia.Id, Guid.NewGuid().ToString("N"), Base.AddDays(7), Base).Value;
        token.MarcarUsado(Base.AddMinutes(1), Guid.NewGuid());

        await using (var seed = fixture.CreateContext())
        {
            seed.RefreshTokenFamilies.Add(familia);
            seed.RefreshTokens.Add(token);
            await seed.SaveChangesAsync();
        }

        int afetadas;
        await using (var ctx = fixture.CreateContext())
        {
            afetadas = await new RefreshTokenRepository(ctx).MarcarUsadoSeNaoUsadoAsync(token.Id, Base.AddMinutes(10), Guid.NewGuid());
        }

        afetadas.Should().Be(0);
    }

    [Fact]
    public async Task RotacionarAsync_DuasRotacoesConcorrentes_UmSucessoOutroReuse()
    {
        // SEC-01: sem o claim atômico, 2 refresh concorrentes do mesmo token emitiriam 2 sucessores
        // (fork de sessão). Com ele, 1 vence e o outro vira reuse (família revogada).
        var (familia, raw) = await SeedSessaoAtivaAsync();
        var agora = Base.AddMinutes(30);

        using var barrier = new Barrier(participantCount: 2);

        async Task<ResultadoRotacao> Rotacionar()
        {
            barrier.SignalAndWait();
            await using var ctx = fixture.CreateContext();
            var resultado = await BuildService(ctx).RotacionarAsync(raw, agora);
            await ctx.SaveChangesAsync(); // o handler real commitaria o sucessor/revogação
            return resultado.Resultado;
        }

        var resultados = await Task.WhenAll(Task.Run(Rotacionar), Task.Run(Rotacionar));

        resultados.Count(r => r == ResultadoRotacao.Sucesso).Should().Be(1);
        resultados.Count(r => r == ResultadoRotacao.ReuseDetectado).Should().Be(1);

        await using var verify = fixture.CreateContext();
        var original = await verify.RefreshTokens.SingleAsync(t => t.FamiliaId == familia.Id && t.SubstituidoPorId != null);
        original.UsadoEm.Should().NotBeNull();
        (await verify.RefreshTokens.CountAsync(t => t.FamiliaId == familia.Id)).Should().BeLessThanOrEqualTo(2);
        var fam = await verify.RefreshTokenFamilies.SingleAsync(f => f.Id == familia.Id);
        fam.MotivoRevogacao.Should().Be(MotivoRevogacaoFamilia.ReuseDetectado);
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

    private static RefreshTokenService BuildService(forzion.tech.Infrastructure.Persistence.AppDbContext ctx)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        return new RefreshTokenService(
            new RefreshTokenRepository(ctx),
            new RefreshTokenFamilyRepository(ctx),
            new ContaRepository(ctx),
            config,
            NullLogger<RefreshTokenService>.Instance);
    }

    private async Task<(RefreshTokenFamily familia, string raw)> SeedSessaoAtivaAsync()
    {
        var conta = Conta.Criar(Email.Criar($"r{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, Base).Value;
        var familia = RefreshTokenFamily.Criar(conta.Id, Base.AddDays(90), Base).Value;
        var raw = Guid.NewGuid().ToString("N");
        var token = RefreshToken.Criar(familia.Id, Hash(raw), Base.AddDays(7), Base).Value;

        await using var seed = fixture.CreateContext();
        seed.Contas.Add(conta);
        seed.RefreshTokenFamilies.Add(familia);
        seed.RefreshTokens.Add(token);
        await seed.SaveChangesAsync();
        return (familia, raw);
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
