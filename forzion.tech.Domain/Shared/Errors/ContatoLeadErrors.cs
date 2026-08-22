namespace forzion.tech.Domain.Shared.Errors;

public static class ContatoLeadErrors
{
    public static Error ValorMuitoLongo => Error.Validation("contato_lead.valor_muito_longo", "O contato deve ter no máximo 320 caracteres.");
    public static Error TelefoneInvalido => Error.Validation("contato_lead.telefone_invalido", "O telefone informado é inválido.");
}
