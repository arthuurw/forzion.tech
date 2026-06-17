using System.Security.Cryptography;
using FluentAssertions;
using forzion.tech.Infrastructure.Services;

namespace forzion.tech.Tests.Infrastructure.Services;

public class MfaSecretProtectorTests
{
    private static byte[] Chave() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void RoundTrip_RevelaTextoOriginal()
    {
        var protector = new MfaSecretProtector(Chave());

        var cifrado = protector.Proteger("JBSWY3DPEHPK3PXP");

        cifrado.Should().NotBe("JBSWY3DPEHPK3PXP");
        protector.Revelar(cifrado).Should().Be("JBSWY3DPEHPK3PXP");
    }

    [Fact]
    public void Proteger_MesmoTexto_ProduzCifradosDistintos()
    {
        var protector = new MfaSecretProtector(Chave());

        protector.Proteger("segredo").Should().NotBe(protector.Proteger("segredo"));
    }

    [Fact]
    public void Ctor_ChaveTamanhoInvalido_Lanca()
    {
        var act = () => new MfaSecretProtector(RandomNumberGenerator.GetBytes(16));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Revelar_ChaveDiferente_Falha()
    {
        var cifrado = new MfaSecretProtector(Chave()).Proteger("segredo");
        var outro = new MfaSecretProtector(Chave());

        var act = () => outro.Revelar(cifrado);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Revelar_ConteudoAdulterado_Falha()
    {
        var protector = new MfaSecretProtector(Chave());
        var bytes = Convert.FromBase64String(protector.Proteger("segredo"));
        bytes[^1] ^= 0xFF;

        var act = () => protector.Revelar(Convert.ToBase64String(bytes));

        act.Should().Throw<CryptographicException>();
    }
}
