namespace forzion.tech.Domain.Shared.Errors;

public static class HealthErrors
{
    public static Error AmbienteObrigatorio => new("health.ambiente_obrigatorio", "O ambiente é obrigatório.");
    public static Error PayloadObrigatorio => new("health.payload_obrigatorio", "O payload é obrigatório.");
    public static Error DestinatarioObrigatorio => new("health.destinatario_obrigatorio", "Uma configuração ativa exige ao menos um destinatário.");
}
