namespace forzion.tech.Domain.Shared.Errors;

public static class SystemUserErrors
{
    public static Error ContaIdInvalido => new("system_user.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error NomeObrigatorio => new("system_user.nome_obrigatorio", "O nome é obrigatório.");
    public static Error NomeMuitoLongo => new("system_user.nome_muito_longo", "O nome deve ter no máximo 100 caracteres.");
}
