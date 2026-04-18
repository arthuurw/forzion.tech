using System.Text.RegularExpressions;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.ValueObjects;

public sealed record Email
{
    private static readonly Regex FormatoValido = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

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

    /// <summary>
    /// Reconstitui um Email a partir de um valor já validado (ex: leitura do banco).
    /// Não reaplica validações.
    /// </summary>
    public static Email Reconstituir(string value) => new(value);

    public override string ToString() => Value;
}
