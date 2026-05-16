using System.Text.RegularExpressions;

namespace forzion.tech.AI.GuardRails;

public static class OutputSanitizer
{
    // Remove markdown images com URL externa — vetor de exfiltração via pixel tracking
    private static readonly Regex ExternalImages = new(
        @"!\[([^\]]*)\]\(https?://[^\)]+\)",
        RegexOptions.Compiled);

    // Remove inline HTML que pode conter scripts ou tracking
    private static readonly Regex InlineHtml = new(
        @"<(?!(?:br|strong|em|code|pre|ul|ol|li|p|h[1-6])\b)[^>]+>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string SanitizeMarkdown(string output)
    {
        var noImages = ExternalImages.Replace(output, "[$1]");
        var noHtml = InlineHtml.Replace(noImages, "");
        return noHtml.Trim();
    }
}
