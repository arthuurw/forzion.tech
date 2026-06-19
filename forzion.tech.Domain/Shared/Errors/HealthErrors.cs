namespace forzion.tech.Domain.Shared.Errors;

public static class HealthErrors
{
    public static Error AmbienteObrigatorio => Error.Validation("health.ambiente_obrigatorio", "O ambiente é obrigatório.");
    public static Error PayloadObrigatorio => Error.Validation("health.payload_obrigatorio", "O payload é obrigatório.");
    public static Error DestinatarioObrigatorio => Error.Validation("health.destinatario_obrigatorio", "Uma configuração ativa exige ao menos um destinatário.");
}
