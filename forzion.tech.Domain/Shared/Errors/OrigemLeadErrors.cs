namespace forzion.tech.Domain.Shared.Errors;

public static class OrigemLeadErrors
{
    public static Error UserAgentMuitoLongo => Error.Validation("origem_lead.user_agent_muito_longo", "O user agent deve ter no máximo 500 caracteres.");
    public static Error AssistenteMuitoLongo => Error.Validation("origem_lead.assistente_muito_longo", "O assistente deve ter no máximo 100 caracteres.");
}
