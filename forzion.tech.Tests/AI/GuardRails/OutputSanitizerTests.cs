using FluentAssertions;
using forzion.tech.AI.GuardRails;

namespace forzion.tech.Tests.AI.GuardRails;

public class OutputSanitizerTests
{
    [Fact]
    public void SanitizeMarkdown_ExternalImage_Replaced()
    {
        var output = "Veja ![tracker](https://evil.com/pixel.gif) aqui";
        var result = OutputSanitizer.SanitizeMarkdown(output);
        result.Should().NotContain("https://");
        result.Should().Contain("[tracker]");
    }

    [Fact]
    public void SanitizeMarkdown_InlineScript_Removed()
    {
        var output = "Texto <script>alert(1)</script> continua";
        var result = OutputSanitizer.SanitizeMarkdown(output);
        result.Should().NotContain("<script>");
        result.Should().NotContain("</script>");
    }

    [Fact]
    public void SanitizeMarkdown_SafeMarkdown_Unchanged()
    {
        var md = "**Treino A**: 3x10 agachamento\n- Série 1: 60kg";
        var result = OutputSanitizer.SanitizeMarkdown(md);
        result.Should().Be(md);
    }

    [Fact]
    public void SanitizeMarkdown_AllowedTags_Preserved()
    {
        var output = "Use <strong>força</strong> e <em>foco</em>";
        var result = OutputSanitizer.SanitizeMarkdown(output);
        result.Should().Contain("<strong>");
        result.Should().Contain("<em>");
    }
}
