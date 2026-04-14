using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.ValueObjects;

public sealed record Slug
{
    public string Value { get; }

    private Slug(string value) => Value = value;

    /// <summary>
    /// Gera um Slug a partir de um nome, aplicando normalização e remoção de acentos.
    /// </summary>
    public static Slug FromNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do tenant é obrigatório para gerar o slug.");

        var slug = nome.ToLowerInvariant()
            .Replace(" ",  "-", StringComparison.Ordinal)
            .Replace("ã", "a", StringComparison.Ordinal)
            .Replace("â", "a", StringComparison.Ordinal)
            .Replace("á", "a", StringComparison.Ordinal)
            .Replace("à", "a", StringComparison.Ordinal)
            .Replace("ê", "e", StringComparison.Ordinal)
            .Replace("é", "e", StringComparison.Ordinal)
            .Replace("í", "i", StringComparison.Ordinal)
            .Replace("õ", "o", StringComparison.Ordinal)
            .Replace("ô", "o", StringComparison.Ordinal)
            .Replace("ó", "o", StringComparison.Ordinal)
            .Replace("ú", "u", StringComparison.Ordinal)
            .Replace("ü", "u", StringComparison.Ordinal)
            .Replace("ç", "c", StringComparison.Ordinal);

        return new Slug(slug);
    }

    /// <summary>
    /// Reconstitui um Slug a partir de um valor já processado (ex: leitura do banco ou sufixo de unicidade).
    /// Não reaplica transformações.
    /// </summary>
    public static Slug Reconstituir(string value) => new(value);

    public override string ToString() => Value;
}
