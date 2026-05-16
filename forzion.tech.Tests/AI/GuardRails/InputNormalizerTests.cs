using FluentAssertions;
using forzion.tech.AI.GuardRails;

namespace forzion.tech.Tests.AI.GuardRails;

public class InputNormalizerTests
{
    [Fact]
    public void NormalizeUnicode_CleanInput_ReturnsSame()
    {
        var result = InputNormalizer.NormalizeUnicode("Qual é meu próximo treino?");
        result.Should().Be("Qual é meu próximo treino?");
    }

    [Fact]
    public void NormalizeUnicode_UnicodeTags_Removed()
    {
        // U+E0000–U+E007F: invisible tag characters used in prompt injection
        var tagChar = "\U000E0041"; // TAG LATIN CAPITAL LETTER A
        var input = $"normal{tagChar}text";
        var result = InputNormalizer.NormalizeUnicode(input);
        result.Should().NotContain(tagChar);
        result.Should().Contain("normal");
    }

    [Fact]
    public void NormalizeUnicode_NfcNormalization_Applied()
    {
        // é as two codepoints (e + combining acute) vs. precomposed
        var decomposed = "é"; // e + combining acute accent
        var result = InputNormalizer.NormalizeUnicode(decomposed);
        result.Should().Be("é"); // precomposed é
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("abcd", 1)]
    [InlineData("abcdefgh", 2)]
    [InlineData("12345678901234", 3)]
    public void EstimateTokens_ReturnsLengthDividedByFour(string input, int expected)
    {
        InputNormalizer.EstimateTokens(input).Should().Be(expected);
    }
}
