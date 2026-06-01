using System.Text.RegularExpressions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.ValueObjects;

public sealed record Email
{
    // NonBacktracking elimina catastrophic backtracking; timeout largo evita falsos
    // RegexMatchTimeoutException em ambientes lentos (CI cold start, Release JIT).
    private static readonly Regex FormatoValido = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(1));

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Criar(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<Email>(EmailErrors.Obrigatorio);

        var normalizado = value.Trim().ToLowerInvariant();

        if (normalizado.Length > 256)
            return Result.Failure<Email>(EmailErrors.MuitoLongo);

        if (!FormatoValido.IsMatch(normalizado))
            return Result.Failure<Email>(EmailErrors.Invalido);

        return Result.Success(new Email(normalizado));
    }

    // Bypassa validações — apenas para reconstituição a partir de dados já persistidos.
    public static Email FromDatabase(string value) => new(value);

    public override string ToString() => Value;
}
