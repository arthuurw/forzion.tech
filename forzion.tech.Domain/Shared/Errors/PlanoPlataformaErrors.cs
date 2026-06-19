namespace forzion.tech.Domain.Shared.Errors;

public static class PlanoPlataformaErrors
{
    public static Error NomeObrigatorio => Error.Validation("plano_plataforma.nome_obrigatorio", "O nome é obrigatório.");
    public static Error NomeVazio => Error.Validation("plano_plataforma.nome_vazio", "O nome não pode ser vazio.");
    public static Error NomeMuitoLongo => Error.Validation("plano_plataforma.nome_muito_longo", "O nome deve ter no máximo 100 caracteres.");
    public static Error MaxAlunosInvalido => Error.Validation("plano_plataforma.max_alunos_invalido", "O limite de alunos deve ser maior que zero.");
    public static Error PrecoNegativo => Error.Validation("plano_plataforma.preco_negativo", "O preço não pode ser negativo.");
    public static Error EliteIndisponivel => Error.Business("plano_plataforma.elite_indisponivel", "O plano Elite está indisponível no momento (em breve).");
}
