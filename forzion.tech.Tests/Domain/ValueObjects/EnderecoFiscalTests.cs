using FluentAssertions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.ValueObjects;

public class EnderecoFiscalTests
{
    private static Result<EnderecoFiscal> Criar(
        string logradouro = "Rua das Flores",
        string numero = "100",
        string bairro = "Centro",
        string ibge = "3550308",
        string uf = "SP",
        string cep = "01001-000",
        string? complemento = null)
        => EnderecoFiscal.Criar(logradouro, numero, bairro, ibge, uf, cep, complemento);

    [Fact]
    public void Criar_DadosValidos_NormalizaCepEUf()
    {
        var result = Criar(cep: "01001-000", uf: "sp");

        result.IsSuccess.Should().BeTrue();
        result.Value.Cep.Should().Be("01001000");
        result.Value.Uf.Should().Be("SP");
        result.Value.CodigoMunicipioIbge.Should().Be("3550308");
    }

    [Fact]
    public void Criar_ComplementoVazio_ViraNull()
    {
        Criar(complemento: "   ").Value.Complemento.Should().BeNull();
    }

    [Theory]
    [InlineData("0100100")]
    [InlineData("010010000")]
    public void Criar_CepForaDe8Digitos_Falha(string cep)
    {
        Criar(cep: cep).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("")]
    public void Criar_UfInvalida_Falha(string uf)
    {
        Criar(uf: uf).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("355030")]
    [InlineData("35503088")]
    public void Criar_IbgeForaDe7Digitos_Falha(string ibge)
    {
        Criar(ibge: ibge).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_LogradouroVazio_Falha(string logradouro)
    {
        Criar(logradouro: logradouro).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_NumeroVazio_Falha()
    {
        Criar(numero: " ").IsFailure.Should().BeTrue();
    }
}
