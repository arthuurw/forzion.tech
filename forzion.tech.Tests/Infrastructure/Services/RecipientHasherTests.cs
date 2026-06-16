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
    public void Hash_MesmoValorEChave_Deterministico()
    {
        var hasher = Build("k1");
        hasher.Hash("user@example.com").Should().Be(hasher.Hash("user@example.com"));
    }

    [Fact]
    public void Hash_NaoVazaValorCru_E64HexChars()
    {
        var hash = Build("k1").Hash("user@example.com");

        hash.Should().NotContain("user@example.com");
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Hash_ChavesDiferentes_ProduzemHashesDiferentes()
    {
        Build("k1").Hash("user@example.com")
            .Should().NotBe(Build("k2").Hash("user@example.com"));
    }
}
