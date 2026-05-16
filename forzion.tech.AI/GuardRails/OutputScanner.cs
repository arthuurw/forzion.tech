using System.Text.RegularExpressions;

namespace forzion.tech.AI.GuardRails;

public sealed record ScanResult(bool HasCritical, IReadOnlyList<string> Findings);

public static class OutputScanner
{
    private static readonly Regex Cpf = new(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex Cnpj = new(@"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex Email = new(@"\b[\w.+-]+@[\w-]+\.[\w.-]+\b", RegexOptions.Compiled);
    private static readonly Regex Card = new(@"\b(?:\d[ -]?){13,19}\b", RegexOptions.Compiled);
    private static readonly Regex ApiKey = new(@"\b(?:sk-|Bearer\s)[A-Za-z0-9\-_]{20,}\b", RegexOptions.Compiled);
    private static readonly Regex ExfiltrationImg = new(@"!\[.*?\]\(https?://[^\)]+\)", RegexOptions.Compiled);

    public static ScanResult Scan(string output)
    {
        var findings = new List<string>();

        if (Cpf.IsMatch(output)) findings.Add("cpf");
        if (Cnpj.IsMatch(output)) findings.Add("cnpj");
        if (Card.IsMatch(output)) findings.Add("card");
        if (ApiKey.IsMatch(output)) findings.Add("api_key");
        if (ExfiltrationImg.IsMatch(output)) findings.Add("exfiltration_img");

        // Email é warning, não crítico — pode aparecer legitimamente
        if (Email.IsMatch(output)) findings.Add("email_warning");

        var critical = new[] { "cpf", "cnpj", "card", "api_key", "exfiltration_img" };
        var hasCritical = findings.Any(f => critical.Contains(f));

        return new ScanResult(hasCritical, findings);
    }
}
