namespace forzion.tech.Domain.Shared.Errors;

public static class AlunoErrors
{
    public static Error ContaIdInvalido => new("aluno.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error NomeObrigatorio => new("aluno.nome_obrigatorio", "O nome é obrigatório.");
    public static Error NomeMuitoLongo => new("aluno.nome_muito_longo", "O nome deve ter no máximo 100 caracteres.");
    public static Error NomeVazio => new("aluno.nome_vazio", "O nome não pode ser vazio.");
    public static Error JaAtivo => new("aluno.ja_ativo", "O aluno já está ativo.");
    public static Error JaInativo => new("aluno.ja_inativo", "O aluno já está inativo.");
    public static Error TelefoneMuitoLongo => new("aluno.telefone_muito_longo", "O telefone deve ter no máximo 20 caracteres.");
}
