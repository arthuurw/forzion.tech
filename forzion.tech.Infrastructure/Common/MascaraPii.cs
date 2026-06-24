using System.Text.RegularExpressions;

namespace forzion.tech.Infrastructure.Common;

public static partial class MascaraPii
{
    public static string Email(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(vazio)";
        var arroba = email.IndexOf('@');
        if (arroba <= 0) return "***";
        return $"{email[0]}***@{email[(arroba + 1)..]}";
    }

    public static string Telefone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "(vazio)";
        return phone.Length <= 4 ? "***" : $"***{phone[^4..]}";
    }

    public static string? Scrub(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        text = EmailRegex().Replace(text, "[email]");
        text = DigitRunRegex().Replace(text, "[num]");
        return text;
    }

    [GeneratedRegex(@"\b[\w.+-]+@[\w-]+\.[\w.-]+\b")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\d{7,}")]
    private static partial Regex DigitRunRegex();
}
