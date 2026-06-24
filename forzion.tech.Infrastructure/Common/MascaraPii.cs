namespace forzion.tech.Infrastructure.Common;

public static class MascaraPii
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
}
