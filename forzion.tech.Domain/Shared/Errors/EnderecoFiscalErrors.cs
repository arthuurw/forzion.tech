namespace forzion.tech.Domain.Shared.Errors;

public static class EnderecoFiscalErrors
{
    public static Error LogradouroObrigatorio => new("endereco_fiscal.logradouro_obrigatorio", "O logradouro é obrigatório.");
    public static Error LogradouroMuitoLongo => new("endereco_fiscal.logradouro_muito_longo", "O logradouro deve ter no máximo 200 caracteres.");
    public static Error NumeroObrigatorio => new("endereco_fiscal.numero_obrigatorio", "O número do endereço é obrigatório.");
    public static Error BairroObrigatorio => new("endereco_fiscal.bairro_obrigatorio", "O bairro é obrigatório.");
    public static Error CepInvalido => new("endereco_fiscal.cep_invalido", "O CEP deve conter 8 dígitos.");
    public static Error UfInvalida => new("endereco_fiscal.uf_invalida", "A UF informada é inválida.");
    public static Error MunicipioIbgeInvalido => new("endereco_fiscal.municipio_ibge_invalido", "O código IBGE do município deve conter 7 dígitos.");
}
