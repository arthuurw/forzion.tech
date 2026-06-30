namespace forzion.tech.Domain.Shared.Errors;

public static class ConsultaCepErrors
{
    public static Error CepInvalido => Error.Validation("consulta_cep.cep_invalido", "O CEP deve conter 8 dígitos.");
    public static Error CepNaoEncontrado => Error.NotFound("consulta_cep.nao_encontrado", "CEP não encontrado.");
    public static Error ServicoIndisponivel => Error.ExternalService("consulta_cep.servico_indisponivel", "Serviço de consulta de CEP indisponível no momento.");
}
