using System.Text.RegularExpressions;

namespace forzion.tech.AI.GuardRails;

public static class PromptInjectionPatterns
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

    private static readonly Regex[] Patterns =
    [
        new(@"ignore\s+(?:all\s+)?(?:previous|prior|above|earlier)\s+(?:instructions?|prompts?|rules?)", RegexOptions.IgnoreCase | RegexOptions.Compiled, Timeout),
        new(@"disregard\s+(?:all\s+)?(?:previous|prior|above)\s+instructions?", RegexOptions.IgnoreCase | RegexOptions.Compiled, Timeout),
        new(@"^\s*(?:system|assistant)\s*[:>]", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled, Timeout),
        new(@"you\s+are\s+now\s+(?:a|an)?\s*\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled, Timeout),
        new(@"\bDAN\b|\bdo\s+any\s*thing\s*now\b|\bDoAnythingNow\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, Timeout),
        new(@"(?:reveal|show|print|repeat|tell\s+me)\s+(?:(?:me|us)\s+)?(?:your|the)\s+(?:system\s+)?(?:prompt|instructions?)", RegexOptions.IgnoreCase | RegexOptions.Compiled, Timeout),
        new(@"</?\s*(?:system|instructions?|admin|developer)\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, Timeout),
        new(@"[A-Za-z0-9+/]{200,}={0,2}", RegexOptions.Compiled, Timeout),
    ];

    public static (bool Detected, string? Pattern) Check(string input)
    {
        try
        {
            foreach (var pattern in Patterns)
            {
                if (pattern.IsMatch(input))
                    return (true, pattern.ToString());
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Timeout na detecção → sinaliza como injeção por segurança (fail-closed)
            return (true, "regex_timeout");
        }

        return (false, null);
    }
}
