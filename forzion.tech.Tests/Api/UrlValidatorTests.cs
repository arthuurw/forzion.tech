using FluentAssertions;
using forzion.tech.Api.Helpers;

namespace forzion.tech.Tests.Api;

public class UrlValidatorTests
{
    private const string UrlBase = "https://app.forzion.tech";

    [Theory]
    [InlineData("https://app.forzion.tech/stripe/retorno")]
    [InlineData("https://app.forzion.tech/stripe/cancelamento?ref=123")]
    [InlineData("https://APP.FORZION.TECH/pagina")] // case-insensitive
    public void IsUrlPermitida_UrlDentroDoDominio_RetornaTrue(string url)
    {
        UrlValidator.IsUrlPermitida(url, UrlBase).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://evil.com/phish")]
    [InlineData("https://app.forzion.tech.evil.com/fake")] // subdomain bypass
    [InlineData("https://outro.dominio.com")]
    public void IsUrlPermitida_UrlForaDoDominio_RetornaFalse(string url)
    {
        UrlValidator.IsUrlPermitida(url, UrlBase).Should().BeFalse();
    }

    [Theory]
    [InlineData("http://app.forzion.tech/pagina")]   // HTTP sem localhost
    [InlineData("ftp://app.forzion.tech/arquivo")]
    [InlineData("javascript:alert(1)")]
    public void IsUrlPermitida_SchemeInseguro_RetornaFalse(string url)
    {
        UrlValidator.IsUrlPermitida(url, UrlBase).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-e-uma-url")]
    [InlineData("//sem-scheme.com")]
    public void IsUrlPermitida_UrlMalformada_RetornaFalse(string url)
    {
        UrlValidator.IsUrlPermitida(url, UrlBase).Should().BeFalse();
    }

    [Theory]
    [InlineData("http://localhost:3000/stripe/retorno", "http://localhost:3000")]
    [InlineData("http://localhost/callback", "http://localhost")]
    public void IsUrlPermitida_LocalhostHttp_RetornaTrue(string url, string urlBase)
    {
        UrlValidator.IsUrlPermitida(url, urlBase).Should().BeTrue();
    }

    [Fact]
    public void IsUrlPermitida_UrlBaseInvalida_RetornaFalse()
    {
        // urlBase deve ser URI válida — string vazia ou malformada rejeita
        UrlValidator.IsUrlPermitida("https://qualquer.com", "").Should().BeFalse();
    }

    [Fact]
    public void IsUrlPermitida_PortaDiferente_RetornaFalse()
    {
        // app.forzion.tech:8443 não é o mesmo origin que app.forzion.tech (443)
        UrlValidator.IsUrlPermitida("https://app.forzion.tech:8443/retorno", UrlBase).Should().BeFalse();
    }
}
