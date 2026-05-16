using FluentAssertions;
using forzion.tech.Api.Extensions;
using Microsoft.AspNetCore.Http;

namespace forzion.tech.Tests.Api.Extensions;

public class PaginationExtensionsTests
{
    private static HttpContext CriarContexto(string? pagina = null, string? tamanhoPagina = null)
    {
        var ctx = new DefaultHttpContext();
        var query = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
        if (pagina is not null) query["pagina"] = pagina;
        if (tamanhoPagina is not null) query["tamanhoPagina"] = tamanhoPagina;
        ctx.Request.Query = new QueryCollection(query);
        return ctx;
    }

    [Fact]
    public void ObterPaginacao_SemParams_RetornaDefaults()
    {
        var result = CriarContexto().ObterPaginacaoDoQuery();
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(20);
    }

    [Fact]
    public void ObterPaginacao_PaginaZero_RetornaUm()
    {
        var result = CriarContexto(pagina: "0").ObterPaginacaoDoQuery();
        result.Pagina.Should().Be(1);
    }

    [Fact]
    public void ObterPaginacao_PaginaValida_RetornaValor()
    {
        // covers the false branch of pagina < 1
        var result = CriarContexto(pagina: "5").ObterPaginacaoDoQuery();
        result.Pagina.Should().Be(5);
    }

    [Fact]
    public void ObterPaginacao_TamanhoPaginaValido_RetornaValor()
    {
        // covers the false branch of tamanhoPagina < 1
        var result = CriarContexto(tamanhoPagina: "50").ObterPaginacaoDoQuery();
        result.TamanhoPagina.Should().Be(50);
    }

    [Fact]
    public void ObterPaginacao_TamanhoPaginaAcimaLimite_Clampeia100()
    {
        var result = CriarContexto(tamanhoPagina: "200").ObterPaginacaoDoQuery();
        result.TamanhoPagina.Should().Be(100);
    }

    [Fact]
    public void ObterPaginacao_ParamsNaoNumericos_RetornaDefaults()
    {
        var result = CriarContexto(pagina: "abc", tamanhoPagina: "xyz").ObterPaginacaoDoQuery();
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(20);
    }
}
