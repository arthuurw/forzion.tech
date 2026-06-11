namespace forzion.tech.Domain.Shared.Errors;

public static class TreinadorErrors
{
    public static Error ContaIdInvalido => new("treinador.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error NomeObrigatorio => new("treinador.nome_obrigatorio", "O nome é obrigatório.");
    public static Error NomeMuitoLongo => new("treinador.nome_muito_longo", "O nome deve ter no máximo 100 caracteres.");
    public static Error NaoAguardandoAprovacaoParaAprovar => new("treinador.nao_aguardando_aprovacao_para_aprovar", "Apenas treinadores aguardando aprovação podem ser aprovados.");
    public static Error NaoAguardandoAprovacaoParaReprovar => new("treinador.nao_aguardando_aprovacao_para_reprovar", "Apenas treinadores aguardando aprovação podem ser reprovados.");
    public static Error JaInativo => new("treinador.ja_inativo", "O treinador já está inativo.");
    public static Error NaoDisponivel => new("treinador.nao_disponivel", "O treinador selecionado não está disponível.");
    public static Error PlanoIdInvalido => new("treinador.plano_id_invalido", "O identificador do plano é inválido.");
    public static Error PlanoTreinadorInativo => new("treinador.plano_treinador_inativo", "Não é possível atribuir plano a um treinador inativo.");
    public static Error ExclusaoApenasInativos => new("treinador.exclusao_apenas_inativos", "Apenas treinadores inativos podem ser excluídos permanentemente.");
    public static Error NomeVazio => new("treinador.nome_vazio", "O nome não pode ser vazio.");
    public static Error NaoAguardandoPagamento => Error.Conflict("treinador.nao_aguardando_pagamento", "Apenas treinadores aguardando pagamento podem ter o pagamento confirmado.");
    public static Error ModoPagamentoInalterado => Error.Business("treinador.modo_inalterado", "O modo de pagamento informado já está em uso.");
    public static Error CooldownModoPagamento(DateTime liberadoEm) =>
        Error.Business("treinador.cooldown_modo_pagamento", $"O modo de pagamento só poderá ser alterado novamente em {liberadoEm:dd/MM/yyyy}.");
    public static Error ConfigureStripePrimeiro => Error.Business("treinador.configure_stripe_primeiro", "Configure sua conta Stripe antes de voltar a receber pela plataforma.");
    public static Error ModoPagamentoInvalido => Error.Business("treinador.modo_pagamento_invalido", "O modo de pagamento informado é inválido.");
    public static Error SemOnboarding => Error.Business("treinador.sem_onboarding", "Configure seus recebimentos (Stripe) antes de aceitar alunos.");
}
