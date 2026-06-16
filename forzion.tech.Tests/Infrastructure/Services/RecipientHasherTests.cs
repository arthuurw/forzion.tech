using FluentAssertions;
using forzion.tech.Application.Settings;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Infrastructure.Services;

public class RecipientHasherTests
{
    private static RecipientHasher Build(string key) =>
        new(Options.Create(new DeliveryLogSettings { RecipientHashKey = key }));

    [Fact]
    public void HashEmail_MesmoValorEChave_Deterministico()
    {
        var hasher = Build("k1");
        hasher.HashEmail("user@example.com").Should().Be(hasher.HashEmail("user@example.com"));
    }

    [Fact]
    public void HashEmail_NaoVazaValorCru_E64HexChars()
    {
        var hash = Build("k1").HashEmail("user@example.com");

        hash.Should().NotContain("user@example.com");
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void HashEmail_ChavesDiferentes_ProduzemHashesDiferentes()
    {
        Build("k1").HashEmail("user@example.com")
            .Should().NotBe(Build("k2").HashEmail("user@example.com"));
    }

    [Fact]
    public void HashEmail_CanonicalizaCasingEEspacos()
    {
        var hasher = Build("k1");
        hasher.HashEmail("  John@Ex.COM ").Should().Be(hasher.HashEmail("john@ex.com"));
    }

    [Fact]
    public void HashTelefone_CanonicalizaParaE164SemMais()
    {
        var hasher = Build("k1");
        hasher.HashTelefone("+55 (11) 99999-8888").Should().Be(hasher.HashTelefone("5511999998888"));
    }

    [Fact]
    public void HashTelefone_LocalBr_PrefixaDdi()
    {
        var hasher = Build("k1");
        hasher.HashTelefone("(11) 99999-8888").Should().Be(hasher.HashTelefone("5511999998888"));
    }
}
