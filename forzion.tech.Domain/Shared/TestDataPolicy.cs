namespace forzion.tech.Domain.Shared;

public static class TestDataPolicy
{
    public const string TestEmailDomain = "@e2e.test";

    public static bool IsTestEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        email.EndsWith(TestEmailDomain, StringComparison.OrdinalIgnoreCase);
}
