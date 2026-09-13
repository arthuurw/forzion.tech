using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.AI.GuardRails;

public static class ToolResponseSanitizer
{
    private const int MaxLength = 10_000;

    private static readonly Regex SuspiciousInstructions = new(
        @"(?:ignore|disregard|forget)\s+(?:previous|prior|above|all)\s+instructions?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SuspiciousTags = new(
        @"</?(?:system|instructions?|admin|tool|assistant)\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExfiltrationPatterns = new(
        @"!\[.*?\]\(https?://[^\)]+\)|<img[^>]*src=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Sanitize(string toolResponse, ILogger? logger = null)
    {
        if (toolResponse.Length > MaxLength)
            toolResponse = toolResponse[..MaxLength] + "\n[truncado]";

        if (SuspiciousInstructions.IsMatch(toolResponse))
            logger?.LogWarning("Padrão de instrução suspeita em tool response");

        if (SuspiciousTags.IsMatch(toolResponse))
            logger?.LogWarning("Tags suspeitas em tool response");

        var sanitized = ExfiltrationPatterns.Replace(toolResponse, "[link removido]");

        return $"<external_data>\n{sanitized}\n</external_data>";
    }
}
