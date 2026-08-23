using FluentAssertions;
using forzion.tech.Domain.Services;

namespace forzion.tech.Tests.Domain.Services;

public class SlotIdTests
{
    private static readonly Guid TreinadorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PacoteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime InicioUtc = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
    private const string HashEsperado = "522d9efa9f290c0be1d72aeab2292b225a19dbd232fbd591c2a985217a136360";

    [Fact]
    public void Calcular_EntradaFixa_ProduzHexExato()
    {
        var slotId = SlotId.Calcular(TreinadorId, PacoteId, InicioUtc);

        slotId.Should().Be(HashEsperado);
    }

    [Fact]
    public void Calcular_NaoUsaGetHashCode_ProduzMesmoValorEmChamadasDistintas()
    {
        var primeiro = SlotId.Calcular(TreinadorId, PacoteId, InicioUtc);
        var segundo = SlotId.Calcular(TreinadorId, PacoteId, InicioUtc);

        primeiro.Should().Be(segundo);
        primeiro.Should().Be(HashEsperado);
    }

    [Fact]
    public void Calcular_InstantesDiferentesDentroDeUmMinuto_ProduzemIdsDiferentes()
    {
        var a = SlotId.Calcular(TreinadorId, PacoteId, InicioUtc);
        var b = SlotId.Calcular(TreinadorId, PacoteId, InicioUtc.AddSeconds(30));

        a.Should().NotBe(b);
    }
}
