using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Persistence.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class MfaRepositoriesTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ContaMfa_RoundTrip()
    {
        var contaId = Guid.NewGuid();
        var mfa = ContaMfa.Criar(contaId, "cifrado==", Agora).Value;

        await using (var ctx = fixture.CreateContext())
        {
            await new ContaMfaRepository(ctx).AdicionarAsync(mfa);
            await ctx.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        var lido = await new ContaMfaRepository(verify).BuscarPorContaIdAsync(contaId);
        lido.Should().NotBeNull();
        lido!.TotpSecretCifrado.Should().Be("cifrado==");
        lido.Habilitado.Should().BeFalse();
    }

    [Fact]
    public async Task MfaRecoveryCode_RoundTrip_Lote()
    {
        var contaId = Guid.NewGuid();
        var codes = Enumerable.Range(0, 10)
            .Select(i => MfaRecoveryCode.Criar(contaId, $"hash-{contaId}-{i}", Agora).Value)
            .ToList();

        await using (var ctx = fixture.CreateContext())
        {
            await new MfaRecoveryCodeRepository(ctx).AdicionarRangeAsync(codes);
            await ctx.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        var lidos = await new MfaRecoveryCodeRepository(verify).ListarPorContaIdAsync(contaId);
        lidos.Should().HaveCount(10);
    }

    [Fact]
    public async Task MfaChallenge_RoundTrip_PorProposito()
    {
        var contaId = Guid.NewGuid();
        var challenge = MfaChallenge.Criar(contaId, $"hash-{contaId}", MfaProposito.StepUp, Agora.AddMinutes(10), Agora).Value;

        await using (var ctx = fixture.CreateContext())
        {
            await new MfaChallengeRepository(ctx).AdicionarAsync(challenge);
            await ctx.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        var repo = new MfaChallengeRepository(verify);
        (await repo.BuscarUltimoPorContaEPropositoAsync(contaId, MfaProposito.StepUp)).Should().NotBeNull();
        (await repo.BuscarUltimoPorContaEPropositoAsync(contaId, MfaProposito.LoginFallback)).Should().BeNull();
    }

    [Fact]
    public async Task TrustedDevice_RoundTrip_PorHash()
    {
        var contaId = Guid.NewGuid();
        var hash = $"hash-{contaId}";
        var device = TrustedDevice.Criar(contaId, hash, Agora.AddDays(30), Agora, "Chrome/Windows").Value;

        await using (var ctx = fixture.CreateContext())
        {
            await new TrustedDeviceRepository(ctx).AdicionarAsync(device);
            await ctx.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        var lido = await new TrustedDeviceRepository(verify).BuscarPorHashAsync(hash);
        lido.Should().NotBeNull();
        lido!.Rotulo.Should().Be("Chrome/Windows");
    }

    [Fact]
    public async Task TrustedDevice_TokenHash_Unico()
    {
        var hash = $"dup-{Guid.NewGuid()}";

        await using (var ctx = fixture.CreateContext())
        {
            ctx.TrustedDevices.Add(TrustedDevice.Criar(Guid.NewGuid(), hash, Agora.AddDays(30), Agora).Value);
            await ctx.SaveChangesAsync();
        }

        await using var ctx2 = fixture.CreateContext();
        ctx2.TrustedDevices.Add(TrustedDevice.Criar(Guid.NewGuid(), hash, Agora.AddDays(30), Agora).Value);

        var act = async () => await ctx2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
