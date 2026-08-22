using FluentAssertions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.ValueObjects;

public class EnderecoPublicoTests
{
    private static Result<EnderecoPublico> Criar(
        string rua = "Rua das Flores",
        string bairro = "Centro",
        string cidade = "São Paulo",
        string estado = "SP",
        string cep = "01001-000",
        string? numero = "100",
        string? complemento = null)
        => EnderecoPublico.Criar(rua, bairro, cidade, estado, cep, numero, complemento);

    [Fact]
    public void Criar_DadosValidos_NormalizaCepEEstado()
    {
        var result = Criar(cep: "01001-000", estado: "sp");

        result.IsSuccess.Should().BeTrue();
        result.Value.Cep.Should().Be("01001000");
        result.Value.Estado.Should().Be("SP");
    }

    [Fact]
    public void Criar_SemNumeroENumeroVazio_ViraNull()
    {
        Criar(numero: null).Value.Numero.Should().BeNull();
        Criar(numero: "   ").Value.Numero.Should().BeNull();
    }

    [Fact]
    public void Criar_ComplementoVazio_ViraNull()
    {
        Criar(complemento: "   ").Value.Complemento.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_RuaVazia_Falha(string rua)
    {
        Criar(rua: rua).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_BairroVazio_Falha(string bairro)
    {
        Criar(bairro: bairro).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_CidadeVazia_Falha(string cidade)
    {
        Criar(cidade: cidade).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("")]
    public void Criar_EstadoInvalido_Falha(string estado)
    {
        Criar(estado: estado).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("0100100")]
    [InlineData("010010000")]
    public void Criar_CepForaDe8Digitos_Falha(string cep)
    {
        Criar(cep: cep).IsFailure.Should().BeTrue();
    }
}
