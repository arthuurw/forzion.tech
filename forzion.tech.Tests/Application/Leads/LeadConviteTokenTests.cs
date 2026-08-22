using FluentAssertions;
using forzion.tech.Application.UseCases.Leads;

namespace forzion.tech.Tests.Application.Leads;

public class LeadConviteTokenTests
{
    [Fact]
    public void Gerar_DevolveHexLowerDe32Bytes()
    {
        var raw = LeadConviteToken.Gerar();

        raw.Should().HaveLength(64);
        raw.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void Gerar_DoisConsecutivos_Diferem()
    {
        var primeiro = LeadConviteToken.Gerar();
        var segundo = LeadConviteToken.Gerar();

        primeiro.Should().NotBe(segundo);
    }

    [Fact]
    public void Hash_EhDeterministico()
    {
        var raw = LeadConviteToken.Gerar();

        LeadConviteToken.Hash(raw).Should().Be(LeadConviteToken.Hash(raw));
    }

    [Fact]
    public void Hash_DevolveHexLowerSha256()
    {
        var hash = LeadConviteToken.Hash("valor-fixo-de-teste");

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void Hash_EntradasDiferentes_ProduzemHashesDiferentes()
    {
        LeadConviteToken.Hash("a").Should().NotBe(LeadConviteToken.Hash("b"));
    }
}
