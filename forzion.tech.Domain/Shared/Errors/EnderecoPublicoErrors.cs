namespace forzion.tech.Domain.Shared.Errors;

public static class EnderecoPublicoErrors
{
    public static Error RuaObrigatoria => Error.Validation("endereco_publico.rua_obrigatoria", "A rua é obrigatória.");
    public static Error BairroObrigatorio => Error.Validation("endereco_publico.bairro_obrigatorio", "O bairro é obrigatório.");
    public static Error CidadeObrigatoria => Error.Validation("endereco_publico.cidade_obrigatoria", "A cidade é obrigatória.");
    public static Error EstadoInvalido => Error.Validation("endereco_publico.estado_invalido", "A UF informada é inválida.");
    public static Error CepInvalido => Error.Validation("endereco_publico.cep_invalido", "O CEP deve conter 8 dígitos.");
}
