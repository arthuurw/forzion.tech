using System.Globalization;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Catálogo central de templates WhatsApp (análogo a <c>EmailTemplates</c>). Cada método
/// devolve um <see cref="WhatsAppTemplateMessage"/> com o nome do template aprovado no painel
/// Meta + variáveis de corpo POSICIONAIS na ordem de <c>{{1}}</c>, <c>{{2}}</c>...
/// Valores monetários em pt-BR (R$ 149,90). A APROVAÇÃO de cada template no Meta Business
/// Manager é ação manual de ops — sem ela a Meta rejeita o envio.
/// </summary>
public static class WhatsAppTemplates
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static string Money(decimal v) => v.ToString("N2", PtBr);

    public static WhatsAppTemplateMessage CobrancaDisponivel(string nomeAluno, decimal valor, MetodoPagamento metodo, string linkPortal)
        => new("cobranca_disponivel", [nomeAluno, Money(valor), metodo == MetodoPagamento.Cartao ? "cartão de crédito" : "Pix", linkPortal]);

    public static WhatsAppTemplateMessage CobrancaFalhou(string nomeAluno, decimal valor, int tentativas, string linkPortal)
        => new("cobranca_falhou", [nomeAluno, Money(valor), tentativas.ToString(PtBr), linkPortal]);

    public static WhatsAppTemplateMessage PagamentoEstornado(string nomeAluno, decimal valor, string linkPortal)
        => new("pagamento_estornado", [nomeAluno, Money(valor), linkPortal]);

    public static WhatsAppTemplateMessage AssinaturaInadimplente(string nomeAluno, string linkPortal)
        => new("assinatura_inadimplente", [nomeAluno, linkPortal]);

    public static WhatsAppTemplateMessage AssinaturaCancelada(string nomeAluno, string linkPortal)
        => new("assinatura_cancelada", [nomeAluno, linkPortal]);

    public static WhatsAppTemplateMessage AssinaturaReativada(string nomeAluno, string linkPortal)
        => new("assinatura_reativada", [nomeAluno, linkPortal]);

    public static WhatsAppTemplateMessage BemVindoAluno(string nomeAluno)
        => new("bem_vindo_aluno", [nomeAluno]);

    public static WhatsAppTemplateMessage AssinaturaCriada(string nomeAluno, string nomePacote, decimal valor)
        => new("assinatura_criada", [nomeAluno, nomePacote, Money(valor)]);

    public static WhatsAppTemplateMessage AlunoInativado(string nomeAluno)
        => new("aluno_inativado", [nomeAluno]);

    public static WhatsAppTemplateMessage VinculoAprovado(string nomeAluno)
        => new("vinculo_aprovado", [nomeAluno]);

    public static WhatsAppTemplateMessage TreinadorAprovado(string nomeTreinador)
        => new("treinador_aprovado", [nomeTreinador]);

    public static WhatsAppTemplateMessage TreinadorReprovado(string nomeTreinador)
        => new("treinador_reprovado", [nomeTreinador]);

    public static WhatsAppTemplateMessage TreinadorInativado(string nomeTreinador)
        => new("treinador_inativado", [nomeTreinador]);

    public static WhatsAppTemplateMessage AlunoCancelouAssinatura(string nomeTreinador, string nomeAluno, decimal valor)
        => new("aluno_cancelou_assinatura", [nomeTreinador, nomeAluno, Money(valor)]);

    public static WhatsAppTemplateMessage PagamentoEmDisputa(string nomeTreinador, string nomeAluno, decimal valor)
        => new("pagamento_em_disputa", [nomeTreinador, nomeAluno, Money(valor)]);

    public static WhatsAppTemplateMessage NovoAlunoPendente(string nomeTreinador, string nomeAluno)
        => new("novo_aluno_pendente", [nomeTreinador, nomeAluno]);

    public static WhatsAppTemplateMessage NovoTreinoDisponivel(string nomeAluno)
        => new("novo_treino_disponivel", [nomeAluno]);
}
