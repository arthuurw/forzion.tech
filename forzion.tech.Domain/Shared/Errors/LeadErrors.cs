namespace forzion.tech.Domain.Shared.Errors;

public static class LeadErrors
{
    public static Error TreinadorIdInvalido => Error.Validation("lead.treinador_id_invalido", "O treinador do lead é obrigatório.");
    public static Error NomeObrigatorio => Error.Validation("lead.nome_obrigatorio", "O nome do lead é obrigatório.");
    public static Error NomeMuitoLongo => Error.Validation("lead.nome_muito_longo", "O nome do lead deve ter no máximo 200 caracteres.");
    public static Error InteresseMuitoLongo => Error.Validation("lead.interesse_muito_longo", "O interesse do lead deve ter no máximo 1000 caracteres.");
}
