using System.Text.RegularExpressions;
using forzion.tech.Domain.Exceptions;

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

    public static Email Criar(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("O e-mail é obrigatório.");

        var normalizado = value.Trim().ToLowerInvariant();

        if (normalizado.Length > 256)
            throw new DomainException("O e-mail deve ter no máximo 256 caracteres.");

        if (!FormatoValido.IsMatch(normalizado))
            throw new DomainException("O e-mail informado é inválido.");

        return new Email(normalizado);
    }

    // Bypassa validações — apenas para reconstituição a partir de dados já persistidos.
    public static Email FromDatabase(string value) => new(value);

    public override string ToString() => Value;
}
