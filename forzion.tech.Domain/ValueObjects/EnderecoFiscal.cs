using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.ValueObjects;

public sealed record EnderecoFiscal
{
    private static readonly HashSet<string> UfsValidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG",
        "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    };

    public string Logradouro { get; }
    public string Numero { get; }
    public string? Complemento { get; }
    public string Bairro { get; }
    public string CodigoMunicipioIbge { get; }
    public string Uf { get; }
    public string Cep { get; }

    private EnderecoFiscal(string logradouro, string numero, string? complemento, string bairro, string codigoMunicipioIbge, string uf, string cep)
    {
        Logradouro = logradouro;
        Numero = numero;
        Complemento = complemento;
        Bairro = bairro;
        CodigoMunicipioIbge = codigoMunicipioIbge;
        Uf = uf;
        Cep = cep;
    }

    public static Result<EnderecoFiscal> Criar(
        string logradouro,
        string numero,
        string bairro,
        string codigoMunicipioIbge,
        string uf,
        string cep,
        string? complemento = null)
    {
        if (string.IsNullOrWhiteSpace(logradouro))
            return Result.Failure<EnderecoFiscal>(EnderecoFiscalErrors.LogradouroObrigatorio);
        if (logradouro.Trim().Length > 200)
            return Result.Failure<EnderecoFiscal>(EnderecoFiscalErrors.LogradouroMuitoLongo);
        if (string.IsNullOrWhiteSpace(numero))
            return Result.Failure<EnderecoFiscal>(EnderecoFiscalErrors.NumeroObrigatorio);
        if (string.IsNullOrWhiteSpace(bairro))
            return Result.Failure<EnderecoFiscal>(EnderecoFiscalErrors.BairroObrigatorio);

        var ibge = SomenteDigitos(codigoMunicipioIbge);
        if (ibge.Length != 7)
            return Result.Failure<EnderecoFiscal>(EnderecoFiscalErrors.MunicipioIbgeInvalido);

        var ufNormalizada = (uf ?? string.Empty).Trim().ToUpperInvariant();
        if (!UfsValidas.Contains(ufNormalizada))
            return Result.Failure<EnderecoFiscal>(EnderecoFiscalErrors.UfInvalida);

        var cepDigitos = SomenteDigitos(cep);
        if (cepDigitos.Length != 8)
            return Result.Failure<EnderecoFiscal>(EnderecoFiscalErrors.CepInvalido);

        var complementoNormalizado = string.IsNullOrWhiteSpace(complemento) ? null : complemento.Trim();

        return Result.Success(new EnderecoFiscal(
            logradouro.Trim(), numero.Trim(), complementoNormalizado, bairro.Trim(), ibge, ufNormalizada, cepDigitos));
    }

    private static string SomenteDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());
}
