using FluentAssertions;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.ValueObjects;

public class DadosFiscaisTests
{
    private static EnderecoFiscal Endereco()
        => EnderecoFiscal.Criar("Rua das Flores", "100", "Centro", "3550308", "SP", "01001000").Value;

    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("11144477735")]
    public void Criar_CpfValido_AceitaESomenteDigitos(string cpf)
    {
        var result = DadosFiscais.Criar(TipoDocumentoFiscal.Cpf, cpf, "João da Silva", Endereco());

        result.IsSuccess.Should().BeTrue();
        result.Value.Documento.Should().MatchRegex("^[0-9]{11}$");
        result.Value.TipoDocumento.Should().Be(TipoDocumentoFiscal.Cpf);
    }

    [Theory]
    [InlineData("12345678900")]
    [InlineData("11111111111")]
    [InlineData("5299822472")]
    public void Criar_CpfInvalido_Falha(string cpf)
    {
        DadosFiscais.Criar(TipoDocumentoFiscal.Cpf, cpf, "João", Endereco()).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("11.222.333/0001-81")]
    [InlineData("11222333000181")]
    public void Criar_CnpjValido_Aceita(string cnpj)
    {
        var result = DadosFiscais.Criar(TipoDocumentoFiscal.Cnpj, cnpj, "Empresa LTDA", Endereco());

        result.IsSuccess.Should().BeTrue();
        result.Value.Documento.Should().Be("11222333000181");
    }

    [Theory]
    [InlineData("11222333000100")]
    [InlineData("00000000000000")]
    [InlineData("1122233300018")]
    public void Criar_CnpjInvalido_Falha(string cnpj)
    {
        DadosFiscais.Criar(TipoDocumentoFiscal.Cnpj, cnpj, "Empresa", Endereco()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_CpfComLength14ComoCnpj_Falha()
    {
        DadosFiscais.Criar(TipoDocumentoFiscal.Cpf, "11222333000181", "Nome", Endereco()).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_RazaoSocialVazia_Falha(string razao)
    {
        DadosFiscais.Criar(TipoDocumentoFiscal.Cpf, "11144477735", razao, Endereco()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_RazaoSocialMuitoLonga_Falha()
    {
        var razao = new string('a', 151);
        DadosFiscais.Criar(TipoDocumentoFiscal.Cpf, "11144477735", razao, Endereco()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_InscricaoMunicipal_SomenteDigitos()
    {
        var result = DadosFiscais.Criar(TipoDocumentoFiscal.Cnpj, "11222333000181", "Empresa", Endereco(), "12.345/6");

        result.Value.InscricaoMunicipal.Should().Be("123456");
    }

    [Fact]
    public void Criar_SemInscricaoMunicipal_Null()
    {
        DadosFiscais.Criar(TipoDocumentoFiscal.Cpf, "11144477735", "João", Endereco()).Value.InscricaoMunicipal.Should().BeNull();
    }
}
