using FluentAssertions;
using forzion.tech.AI.GuardRails;

namespace forzion.tech.Tests.AI.GuardRails;

public class ToolResponseSanitizerTests
{
    [Fact]
    public void Sanitize_WrapsInExternalDataTag()
    {
        var result = ToolResponseSanitizer.Sanitize("dado qualquer");
        result.Should().StartWith("<external_data>");
        result.Should().EndWith("</external_data>");
    }

    [Fact]
    public void Sanitize_LongResponse_Truncated()
    {
        var longInput = new string('x', 11_000);
        var result = ToolResponseSanitizer.Sanitize(longInput);
        result.Should().Contain("[truncado]");
        // Content inside tag is limited
        result.Length.Should().BeLessThan(11_000 + 100);
    }

    [Fact]
    public void Sanitize_ExfiltrationImage_Replaced()
    {
        var input = "resultado ![bad](https://evil.com/p.gif) fim";
        var result = ToolResponseSanitizer.Sanitize(input);
        result.Should().NotContain("https://evil.com");
        result.Should().Contain("[link removido]");
    }

    [Fact]
    public void Sanitize_CleanData_PreservedInsideTag()
    {
        var input = "Treino A: 3x10 agachamento";
        var result = ToolResponseSanitizer.Sanitize(input);
        result.Should().Contain("Treino A: 3x10 agachamento");
    }
}
