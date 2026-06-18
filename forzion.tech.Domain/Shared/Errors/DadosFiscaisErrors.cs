namespace forzion.tech.Domain.Shared.Errors;

public static class DadosFiscaisErrors
{
    public static Error EnderecoObrigatorio => new("dados_fiscais.endereco_obrigatorio", "O endereço fiscal é obrigatório.");
    public static Error RazaoSocialObrigatoria => new("dados_fiscais.razao_social_obrigatoria", "A razão social ou nome do tomador é obrigatório.");
    public static Error RazaoSocialMuitoLonga => new("dados_fiscais.razao_social_muito_longa", "A razão social deve ter no máximo 150 caracteres.");
    public static Error TipoDocumentoInvalido => new("dados_fiscais.tipo_documento_invalido", "O tipo de documento é inválido.");
    public static Error DocumentoInvalido => new("dados_fiscais.documento_invalido", "O CPF/CNPJ informado é inválido.");
}
