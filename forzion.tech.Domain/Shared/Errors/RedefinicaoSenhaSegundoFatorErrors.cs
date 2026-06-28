namespace forzion.tech.Domain.Shared.Errors;

public static class RedefinicaoSenhaSegundoFatorErrors
{
    public static Error ContaIdInvalido => Error.Validation("auth_reset.segundo_fator_conta_id_invalido", "O identificador da conta é inválido.");
    public static Error Bloqueado => Error.Business("auth_reset.segundo_fator_bloqueado", "Muitas tentativas de verificação. Aguarde alguns minutos e solicite um novo link de redefinição.");
}
