namespace forzion.tech.Application.Results;

public sealed record Error(string Code, string Message)
{
    public static Error Business(string message) => new("business_error", message);
}
