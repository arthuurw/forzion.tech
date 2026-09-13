using System.Text.RegularExpressions;

namespace forzion.tech.AI.GuardRails;

public sealed record ScanResult(bool HasCritical, IReadOnlyList<string> Findings);

public static class OutputScanner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);

    private static readonly Regex Cpf = new(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled, Timeout);
    private static readonly Regex Cnpj = new(@"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b", RegexOptions.Compiled, Timeout);
    private static readonly Regex Email = new(@"\b[\w.+-]+@[\w-]+\.[\w.-]+\b", RegexOptions.Compiled, Timeout);
    private static readonly Regex Card = new(@"\b(?:\d[ -]?){13,19}\b", RegexOptions.Compiled, Timeout);
    private static readonly Regex ApiKey = new(@"\b(?:sk-|Bearer\s)[A-Za-z0-9\-_]{20,}\b", RegexOptions.Compiled, Timeout);
    private static readonly Regex ExfiltrationImg = new(@"!\[.*?\]\(https?://[^\)]+\)", RegexOptions.Compiled, Timeout);

    public static ScanResult Scan(string output)
    {
        var findings = new List<string>();

        try
        {
            if (Cpf.IsMatch(output)) findings.Add("cpf");
            if (Cnpj.IsMatch(output)) findings.Add("cnpj");
            if (Card.IsMatch(output)) findings.Add("card");
            if (ApiKey.IsMatch(output)) findings.Add("api_key");
            if (ExfiltrationImg.IsMatch(output)) findings.Add("exfiltration_img");
            if (Email.IsMatch(output)) findings.Add("email_warning");
        }
        catch (RegexMatchTimeoutException)
        {
            // Timeout na varredura → trata como crítico por segurança (fail-closed)
            findings.Add("scan_timeout");
        }

        var critical = new[] { "cpf", "cnpj", "card", "api_key", "exfiltration_img", "scan_timeout" };
        var hasCritical = findings.Any(f => critical.Contains(f));

        return new ScanResult(hasCritical, findings);
    }
}
