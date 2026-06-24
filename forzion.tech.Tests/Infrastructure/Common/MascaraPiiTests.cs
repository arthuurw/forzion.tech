using FluentAssertions;
using forzion.tech.Infrastructure.Common;

namespace forzion.tech.Tests.Infrastructure.Common;

public class MascaraPiiTests
{
    [Fact]
    public void Email_EmailNormal_MascaraIdentificadorEMantemDominio()
    {
        MascaraPii.Email("arthur@dominio.com").Should().Be("a***@dominio.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Email_EmailVazio_RetornaVazio(string email)
    {
        MascaraPii.Email(email).Should().Be("(vazio)");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("@x")]
    public void Email_EmailMalformadoSemArrobaValido_RetornaAsteriscos(string email)
    {
        MascaraPii.Email(email).Should().Be("***");
    }

    [Fact]
    public void Telefone_TelefoneNormal_MascaraPrefixoEMantemUltimosQuatroDigitos()
    {
        MascaraPii.Telefone("+5511998887766").Should().Be("***7766");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Telefone_TelefoneVazio_RetornaVazio(string phone)
    {
        MascaraPii.Telefone(phone).Should().Be("(vazio)");
    }

    [Fact]
    public void Telefone_TelefoneCurtoAteCincoCaracteres_RetornaAsteriscos()
    {
        MascaraPii.Telefone("123").Should().Be("***");
    }
}
