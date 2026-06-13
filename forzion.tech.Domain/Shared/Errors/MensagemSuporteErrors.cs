using forzion.tech.Domain.Entities;

namespace forzion.tech.Domain.Shared.Errors;

public static class MensagemSuporteErrors
{
    public static Error ContaIdInvalido => Error.Validation("suporte.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error CategoriaInvalida => Error.Validation("suporte.categoria_invalida", "A categoria informada é inválida.");
    public static Error AssuntoObrigatorio => Error.Validation("suporte.assunto_obrigatorio", "O assunto é obrigatório.");
    public static Error AssuntoForaDoTamanho => Error.Validation("suporte.assunto_tamanho", $"O assunto deve ter entre {MensagemSuporte.AssuntoMinLength} e {MensagemSuporte.AssuntoMaxLength} caracteres.");
    public static Error DescricaoObrigatoria => Error.Validation("suporte.descricao_obrigatoria", "A descrição é obrigatória.");
    public static Error DescricaoForaDoTamanho => Error.Validation("suporte.descricao_tamanho", $"A descrição deve ter entre {MensagemSuporte.DescricaoMinLength} e {MensagemSuporte.DescricaoMaxLength} caracteres.");
}
