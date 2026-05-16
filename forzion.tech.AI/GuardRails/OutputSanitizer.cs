using System.Text.RegularExpressions;

namespace forzion.tech.AI.GuardRails;

public static class OutputSanitizer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);

    // Remove markdown images com URL externa — vetor de exfiltração via pixel tracking
    private static readonly Regex ExternalImages = new(
        @"!\[([^\]]*)\]\(https?://[^\)]+\)",
        RegexOptions.Compiled, Timeout);

    // Remove inline HTML que pode conter scripts ou tracking
    private static readonly Regex InlineHtml = new(
        @"<(?!(?:br|strong|em|code|pre|ul|ol|li|p|h[1-6])\b)[^>]+>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, Timeout);

    public static string SanitizeMarkdown(string output)
    {
        try
        {
            var noImages = ExternalImages.Replace(output, "[$1]");
            var noHtml = InlineHtml.Replace(noImages, "");
            return noHtml.Trim();
        }
        catch (RegexMatchTimeoutException)
        {
            // Timeout na sanitização → retorna string vazia (ativa safety net no endpoint)
            return string.Empty;
        }
    }
}
