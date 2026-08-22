using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.ValueObjects;

public sealed record EnderecoPublico
{
    public string Rua { get; }
    public string? Numero { get; }
    public string? Complemento { get; }
    public string Bairro { get; }
    public string Cidade { get; }
    public string Estado { get; }
    public string Cep { get; }

    private EnderecoPublico(string rua, string? numero, string? complemento, string bairro, string cidade, string estado, string cep)
    {
        Rua = rua;
        Numero = numero;
        Complemento = complemento;
        Bairro = bairro;
        Cidade = cidade;
        Estado = estado;
        Cep = cep;
    }

    public static Result<EnderecoPublico> Criar(
        string rua,
        string bairro,
        string cidade,
        string estado,
        string cep,
        string? numero = null,
        string? complemento = null)
    {
        if (string.IsNullOrWhiteSpace(rua))
            return Result.Failure<EnderecoPublico>(EnderecoPublicoErrors.RuaObrigatoria);
        if (string.IsNullOrWhiteSpace(bairro))
            return Result.Failure<EnderecoPublico>(EnderecoPublicoErrors.BairroObrigatorio);
        if (string.IsNullOrWhiteSpace(cidade))
            return Result.Failure<EnderecoPublico>(EnderecoPublicoErrors.CidadeObrigatoria);

        var estadoNormalizado = (estado ?? string.Empty).Trim().ToUpperInvariant();
        if (!UfsBrasileiras.Validas.Contains(estadoNormalizado))
            return Result.Failure<EnderecoPublico>(EnderecoPublicoErrors.EstadoInvalido);

        var cepDigitos = Digitos.Apenas(cep);
        if (cepDigitos.Length != 8)
            return Result.Failure<EnderecoPublico>(EnderecoPublicoErrors.CepInvalido);

        var numeroNormalizado = string.IsNullOrWhiteSpace(numero) ? null : numero.Trim();
        var complementoNormalizado = string.IsNullOrWhiteSpace(complemento) ? null : complemento.Trim();

        return Result.Success(new EnderecoPublico(
            rua.Trim(), numeroNormalizado, complementoNormalizado, bairro.Trim(), cidade.Trim(), estadoNormalizado, cepDigitos));
    }
}
