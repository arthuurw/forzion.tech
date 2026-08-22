using FluentAssertions;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.ValueObjects;

public class ContatoLeadTests
{
    [Fact]
    public void Criar_EmailComMaiusculasEEspacos_Normaliza()
    {
        var r = ContatoLead.Criar(TipoContatoLead.Email, "  USER@Example.COM  ");

        r.IsSuccess.Should().BeTrue();
        r.Value.Tipo.Should().Be(TipoContatoLead.Email);
        r.Value.Valor.Should().Be("user@example.com");
    }

    [Fact]
    public void Criar_EmailInvalido_Falha()
    {
        var r = ContatoLead.Criar(TipoContatoLead.Email, "nao-e-email");

        r.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("(11) 99999-9999", "5511999999999")]
    [InlineData("11999999999", "5511999999999")]
    public void Criar_TelefoneLocalBrasileiro_NormalizaParaE164(string entrada, string esperado)
    {
        var r = ContatoLead.Criar(TipoContatoLead.Telefone, entrada);

        r.IsSuccess.Should().BeTrue();
        r.Value.Tipo.Should().Be(TipoContatoLead.Telefone);
        r.Value.Valor.Should().Be(esperado);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("")]
    public void Criar_TelefoneNaoNormalizavel_Falha(string entrada)
    {
        var r = ContatoLead.Criar(TipoContatoLead.Telefone, entrada);

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_WhatsAppUsaMesmaNormalizacaoDoTelefone()
    {
        var r = ContatoLead.Criar(TipoContatoLead.WhatsApp, "(11) 99999-9999");

        r.IsSuccess.Should().BeTrue();
        r.Value.Tipo.Should().Be(TipoContatoLead.WhatsApp);
        r.Value.Valor.Should().Be("5511999999999");
    }

    [Fact]
    public void Criar_ValorAcimaDe320Caracteres_Falha()
    {
        var valor = new string('a', 310) + "@" + new string('b', 15) + ".com";

        var r = ContatoLead.Criar(TipoContatoLead.Email, valor);

        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Contain("320");
    }
}
