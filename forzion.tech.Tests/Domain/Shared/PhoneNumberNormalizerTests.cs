using FluentAssertions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Tests.Domain.Shared;

public class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizar_ValorAusenteOuVazio_RetornaNull(string? valor)
    {
        PhoneNumberNormalizer.Normalizar(valor).Should().BeNull();
    }

    [Fact]
    public void Normalizar_SemDigitos_RetornaNull()
    {
        PhoneNumberNormalizer.Normalizar("abc-def").Should().BeNull();
    }

    [Theory]
    [InlineData("5511999999999", "5511999999999")]
    [InlineData("+55 (11) 99999-9999", "5511999999999")]
    [InlineData("55 11 9999-9999", "551199999999")]
    public void Normalizar_JaComDdiBrasil_MantemComoEsta(string entrada, string esperado)
    {
        PhoneNumberNormalizer.Normalizar(entrada).Should().Be(esperado);
    }

    [Theory]
    [InlineData("(11) 99999-9999", "5511999999999")]
    [InlineData("1199999999", "551199999999")]
    public void Normalizar_LocalBrasileiro_PrefixaDdi55(string entrada, string esperado)
    {
        PhoneNumberNormalizer.Normalizar(entrada).Should().Be(esperado);
    }

    [Theory]
    [InlineData("+44 20 7946 0958", "442079460958")]
    public void Normalizar_InternacionalPlausivel_MantemDigitos(string entrada, string esperado)
    {
        PhoneNumberNormalizer.Normalizar(entrada).Should().Be(esperado);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234567890123456")]
    public void Normalizar_ComprimentoImplausivel_RetornaNull(string entrada)
    {
        PhoneNumberNormalizer.Normalizar(entrada).Should().BeNull();
    }
}
