using FluentAssertions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.ValueObjects;

public class YouTubeVideoIdTests
{
    [Theory]
    [InlineData("dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL123&index=2", "dQw4w9WgXcQ")]
    [InlineData("  https://www.youtube.com/watch?v=dQw4w9WgXcQ  ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("a_B-c1D2e3F", "a_B-c1D2e3F")]
    public void Criar_ComEntradaValida_ExtraiId(string entrada, string esperado)
    {
        var result = YouTubeVideoId.Criar(entrada);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(esperado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dQw4w9WgXc")]
    [InlineData("dQw4w9WgXcQX")]
    [InlineData("https://vimeo.com/123456789")]
    [InlineData("not a url at all")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    public void Criar_ComEntradaInvalida_RetornaFailure(string entrada)
    {
        var result = YouTubeVideoId.Criar(entrada);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("exercicio.video_url_invalida");
    }

    [Fact]
    public void Criar_ExtraiIdMesmoDeHostDesconhecido_PorqueEmbedSempreForcaYouTubeNoCookie()
    {
        var result = YouTubeVideoId.Criar("https://example.com/watch?v=dQw4w9WgXcQ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public void IgualdadePorValor()
    {
        var a = YouTubeVideoId.Criar("dQw4w9WgXcQ").Value;
        var b = YouTubeVideoId.Criar("https://youtu.be/dQw4w9WgXcQ").Value;

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
