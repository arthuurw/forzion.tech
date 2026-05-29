using FluentAssertions;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.Domain.Enums;

public class TierPlanoExtensionsTests
{
    // PermiteEmail: tier >= Pro
    [Theory]
    [InlineData(TierPlano.Free, false)]
    [InlineData(TierPlano.Basic, false)]
    [InlineData(TierPlano.Pro, true)]
    [InlineData(TierPlano.ProPlus, true)]
    [InlineData(TierPlano.Elite, true)]
    public void PermiteEmail_RetornaEsperado(TierPlano tier, bool esperado)
    {
        tier.PermiteEmail().Should().Be(esperado);
    }

    // PermiteWhatsApp: tier >= ProPlus
    [Theory]
    [InlineData(TierPlano.Free, false)]
    [InlineData(TierPlano.Basic, false)]
    [InlineData(TierPlano.Pro, false)]
    [InlineData(TierPlano.ProPlus, true)]
    [InlineData(TierPlano.Elite, true)]
    public void PermiteWhatsApp_RetornaEsperado(TierPlano tier, bool esperado)
    {
        tier.PermiteWhatsApp().Should().Be(esperado);
    }
}
