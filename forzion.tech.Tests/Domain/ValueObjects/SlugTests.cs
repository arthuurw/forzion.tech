using FluentAssertions;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.ValueObjects;

public class SlugTests
{
    [Fact]
    public void FromNome_ConverteMaiusculasParaMinusculas()
    {
        var slug = Slug.FromNome("ACADEMIA FORCA");
        slug.Value.Should().Be("academia-forca");
    }

    [Fact]
    public void FromNome_SubstituiEspacosPorHifen()
    {
        var slug = Slug.FromNome("academia de musculacao");
        slug.Value.Should().Be("academia-de-musculacao");
    }

    [Theory]
    [InlineData("ã", "a")]
    [InlineData("â", "a")]
    [InlineData("á", "a")]
    [InlineData("à", "a")]
    [InlineData("ê", "e")]
    [InlineData("é", "e")]
    [InlineData("í", "i")]
    [InlineData("õ", "o")]
    [InlineData("ô", "o")]
    [InlineData("ó", "o")]
    [InlineData("ú", "u")]
    [InlineData("ü", "u")]
    [InlineData("ç", "c")]
    public void FromNome_RemoveAcento(string entrada, string esperado)
    {
        var slug = Slug.FromNome(entrada);
        slug.Value.Should().Be(esperado);
    }

    [Fact]
    public void FromNome_ComNomeComplexo_TransformaCorretamente()
    {
        var slug = Slug.FromNome("Força & Condição");
        slug.Value.Should().Be("forca-&-condicao");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromNome_ComNomeVazio_LancaDomainException(string nome)
    {
        var act = () => Slug.FromNome(nome);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reconstituir_RetornaValorSemTransformacao()
    {
        var slug = Slug.Reconstituir("meu-slug-existente");
        slug.Value.Should().Be("meu-slug-existente");
    }

    [Fact]
    public void ToString_RetornaValue()
    {
        var slug = Slug.FromNome("academia");
        slug.ToString().Should().Be("academia");
    }
}
