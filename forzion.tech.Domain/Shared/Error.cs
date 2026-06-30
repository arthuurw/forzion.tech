namespace forzion.tech.Domain.Shared;

/// <summary>
/// Classifica o erro de negócio p/ mapeamento HTTP consistente (ver ResultExtensions).
/// Default <see cref="ErrorType.Business"/> → 422 (preserva comportamento histórico).
/// </summary>
public enum ErrorType
{
    Business,
    Validation,
    NotFound,
    Conflict,
    ExternalService
}

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Business)
{
    public static Error Business(string code, string message) => new(code, message);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error ExternalService(string code, string message) => new(code, message, ErrorType.ExternalService);
}
