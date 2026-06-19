namespace forzion.tech.Domain.Shared.Errors;

public static class AlunoErrors
{
    public static Error ContaIdInvalido => Error.Validation("aluno.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error NomeObrigatorio => Error.Validation("aluno.nome_obrigatorio", "O nome é obrigatório.");
    public static Error NomeMuitoLongo => Error.Validation("aluno.nome_muito_longo", "O nome deve ter no máximo 100 caracteres.");
    public static Error NomeVazio => Error.Validation("aluno.nome_vazio", "O nome não pode ser vazio.");
    public static Error JaAtivo => Error.Conflict("aluno.ja_ativo", "O aluno já está ativo.");
    public static Error JaInativo => Error.Conflict("aluno.ja_inativo", "O aluno já está inativo.");
    public static Error TelefoneMuitoLongo => Error.Validation("aluno.telefone_muito_longo", "O telefone deve ter no máximo 20 caracteres.");
    public static Error ConsentimentoSaudeObrigatorio => Error.Validation("aluno.consentimento_saude_obrigatorio", "Consentimento explícito para tratamento de dados de saúde é obrigatório (LGPD art. 11).");
}
