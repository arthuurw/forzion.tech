using System.Globalization;
using System.Net;
using System.Text;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Infrastructure.Notifications.Email;

internal static class EmailTemplates
{
    private static string Layout(string titulo, string corpo) => $"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;padding:0;background:#F5F5F5;font-family:Arial,sans-serif">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:32px 16px">
              <table width="560" cellpadding="0" cellspacing="0" style="background:#fff;border-radius:8px;overflow:hidden">
                <tr><td style="background:#F5C400;padding:24px 32px">
                  <span style="font-size:20px;font-weight:bold;color:#1A1A1A">forzion.tech</span>
                </td></tr>
                <tr><td style="padding:32px">
                  <h2 style="margin:0 0 16px;color:#1A1A1A;font-size:18px">{titulo}</h2>
                  {corpo}
                  <hr style="margin:32px 0;border:none;border-top:1px solid #eee">
                  <p style="margin:0;font-size:12px;color:#999">
                    Este é um e-mail automático. Não responda a esta mensagem.
                  </p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string Enc(string? valor) => WebUtility.HtmlEncode(valor) ?? string.Empty;

    public static string NovoTreinoDisponivel(string nomeAluno) =>
        Layout(
            "Novo treino disponível",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Seu treinador acabou de disponibilizar um <strong>novo treino</strong> para você.
              Acesse a plataforma e comece agora.
            </p>
            <a href="https://forzion.tech/login"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver treino
            </a>
            """);

    public static string LembreteLeve(string nomeAluno) =>
        Layout(
            "Bora treinar?",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Faz alguns dias que você não registra um treino. Que tal manter o ritmo e voltar hoje?
            </p>
            <a href="https://forzion.tech/login"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Registrar treino
            </a>
            """);

    public static string Recuperacao(string nomeAluno) =>
        Layout(
            "Vamos retomar",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Faz um tempo desde o seu último treino. Bora retomar hoje e recuperar o foco — um passo de cada vez.
            </p>
            <a href="https://forzion.tech/login"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Voltar a treinar
            </a>
            """);

    public static string DigestTreinador(string nomeTreinador, int treinaram, int naoTreinaram) =>
        Layout(
            "Resumo de aderência do dia",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeTreinador)}</strong>!</p>
            <p style="color:#444;line-height:1.6">Veja como foi a aderência dos seus alunos hoje:</p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0">
              <tr><td style="padding:4px 0;color:#444"><strong>{treinaram}</strong> aluno(s) treinaram</td></tr>
              <tr><td style="padding:4px 0;color:#444"><strong>{naoTreinaram}</strong> aluno(s) não treinaram</td></tr>
            </table>
            <a href="https://forzion.tech/login"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver detalhes
            </a>
            """);

    public static string TreinadorAprovado(string nome) =>
        Layout(
            "Conta aprovada!",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nome)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua conta de treinador foi <strong style="color:#2e7d32">aprovada</strong>.
              Você já pode acessar a plataforma e começar a cadastrar seus alunos.
            </p>
            <a href="https://forzion.tech/login"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Acessar plataforma
            </a>
            """);

    public static string TreinadorReprovado(string nome) =>
        Layout(
            "Cadastro não aprovado",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nome)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Infelizmente seu cadastro de treinador <strong style="color:#c62828">não foi aprovado</strong>
              neste momento. Entre em contato com nosso suporte para mais informações.
            </p>
            """);

    public static string TreinadorInativado(string nome) =>
        Layout(
            "Conta inativada",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nome)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua conta de treinador foi <strong style="color:#c62828">inativada</strong>.
              Caso acredite que isso foi um engano, entre em contato com nosso suporte.
            </p>
            """);

    public static string VinculoAprovado(string nomeAluno, string nomeTreinador) =>
        Layout(
            "Vínculo aprovado!",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              <strong>{Enc(nomeTreinador)}</strong> aprovou seu vínculo.
              Você já pode acessar suas fichas de treino na plataforma.
            </p>
            <a href="https://forzion.tech/login"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver fichas
            </a>
            """);

    public static string BemVindoAluno(string nome) =>
        Layout(
            "Boas-vindas à forzion.tech!",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nome)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Seu cadastro foi realizado com sucesso. Aguarde a aprovação de quem vai te treinar
              para acessar suas fichas de treino.
            </p>
            <a href="https://forzion.tech/login"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Acessar plataforma
            </a>
            """);

    public static string AlunoInativado(string nome) =>
        Layout(
            "Conta inativada",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nome)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua conta foi <strong style="color:#c62828">inativada</strong>.
              Caso acredite que isso foi um engano, entre em contato com quem te treina.
            </p>
            """);

    public static string RedefinirSenha(string email, string resetLink) =>
        Layout(
            "Redefinição de senha",
            $"""
            <p style="color:#444;line-height:1.6">Olá!</p>
            <p style="color:#444;line-height:1.6">
              Recebemos uma solicitação de redefinição de senha para a conta associada ao e-mail
              <strong>{email}</strong>.
            </p>
            <p style="color:#444;line-height:1.6">
              Clique no botão abaixo para criar uma nova senha. O link é válido por <strong>1 hora</strong>.
            </p>
            <a href="{resetLink}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Redefinir senha
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Se você não solicitou a redefinição, ignore este e-mail. Sua senha permanece a mesma.
            </p>
            """);

    public static string TrocaEmailCodigo(string codigo) =>
        Layout(
            "Confirmação de troca de e-mail",
            $"""
            <p style="color:#444;line-height:1.6">Olá!</p>
            <p style="color:#444;line-height:1.6">
              Recebemos uma solicitação para associar este endereço à sua conta forzion.tech.
              Use o código abaixo para confirmar a troca. Ele é válido por <strong>30 minutos</strong>.
            </p>
            <p style="font-size:24px;font-weight:bold;letter-spacing:4px;color:#1A1A1A;text-align:center;margin:24px 0;word-break:break-all">{codigo}</p>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Se você não solicitou a troca de e-mail, ignore esta mensagem. Seu e-mail permanece o mesmo.
            </p>
            """);

    public static string CodigoMfa(string codigo) =>
        Layout(
            "Seu código de verificação",
            $"""
            <p style="color:#444;line-height:1.6">Olá!</p>
            <p style="color:#444;line-height:1.6">
              Use o código abaixo para concluir sua verificação. Ele é válido por <strong>10 minutos</strong>.
            </p>
            <p style="font-size:32px;font-weight:bold;letter-spacing:8px;color:#1A1A1A;text-align:center;margin:24px 0">{codigo}</p>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Se você não solicitou este código, ignore este e-mail e considere trocar sua senha.
            </p>
            """);

    public static string VerificarEmail(string email, string verifyLink) =>
        Layout(
            "Confirme seu e-mail",
            $"""
            <p style="color:#444;line-height:1.6">Olá!</p>
            <p style="color:#444;line-height:1.6">
              Falta pouco para ativar sua conta associada ao e-mail <strong>{email}</strong>.
            </p>
            <p style="color:#444;line-height:1.6">
              Clique no botão abaixo para confirmar seu e-mail. O link é válido por <strong>24 horas</strong>.
            </p>
            <a href="{verifyLink}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Verificar e-mail
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Se você não criou esta conta, ignore este e-mail.
            </p>
            """);

    public static string CobrancaDisponivel(string nomeAluno, decimal valor, MetodoPagamento metodo, string linkPortal)
    {
        var metodoLabel = metodo == MetodoPagamento.Cartao ? "cartão de crédito" : "Pix";
        // Formatação pt-BR explícita (R$ 149,90) — independe de culture do processo.
        var valorFormatado = valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
        return Layout(
            "Cobrança disponível",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Uma nova cobrança da sua assinatura está disponível.
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Valor</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">R$ {valorFormatado}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Método</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{metodoLabel}</td>
              </tr>
            </table>
            <p style="color:#444;line-height:1.6">
              Acesse o portal para concluir o pagamento.
            </p>
            <a href="{linkPortal}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver pagamento
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Em caso de dúvidas, fale com quem te treina.
            </p>
            """);
    }

    public static string CobrancaProxima(string nome, decimal valor, DateTime dataProximaCobranca, string linkPortal)
    {
        var ptBr = CultureInfo.GetCultureInfo("pt-BR");
        var valorFormatado = valor.ToString("N2", ptBr);
        var dataFormatada = dataProximaCobranca.ToString("dd/MM/yyyy", ptBr);
        var dataLimite = dataProximaCobranca.AddDays(-1).ToString("dd/MM/yyyy", ptBr);
        return Layout(
            "Sua assinatura renova em 3 dias",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nome)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Seu plano forzion.tech renova automaticamente em <strong>3 dias</strong>.
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Valor</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">R$ {valorFormatado}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Data da cobrança</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{dataFormatada}</td>
              </tr>
            </table>
            <p style="color:#444;line-height:1.6">
              Se desejar, você pode cancelar até <strong>{dataLimite}</strong> pelo portal — sem nova cobrança.
            </p>
            <a href="{linkPortal}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Acessar portal
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Em caso de dúvidas, fale com o suporte forzion.tech.
            </p>
            """);
    }

    public static string CobrancaFalhou(string nomeAluno, decimal valor, int tentativasFalhas, string linkPortal)
    {
        // Formatação pt-BR explícita (R$ 149,90) — independe de culture do processo.
        var valorFormatado = valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
        var (titulo, mensagem, corEnfase) = tentativasFalhas switch
        {
            <= 1 => (
                "Cobrança não processada",
                "Não conseguimos processar sua cobrança. Tente outro método de pagamento ou atualize seus dados no portal.",
                "#ef6c00"),
            2 => (
                "Segunda tentativa falhou",
                "Segunda tentativa falhou. Atualize seu cartão antes da próxima tentativa para evitar a suspensão da sua assinatura.",
                "#ef6c00"),
            _ => (
                "Última tentativa antes do bloqueio",
                "Última tentativa antes do bloqueio da sua conta. Regularize seu pagamento agora para manter o acesso à plataforma.",
                "#c62828")
        };
        return Layout(
            titulo,
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              <strong style="color:{corEnfase}">{mensagem}</strong>
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Valor</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">R$ {valorFormatado}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Tentativas</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{tentativasFalhas}</td>
              </tr>
            </table>
            <a href="{linkPortal}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Atualizar pagamento
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Em caso de dúvidas, fale com quem te treina.
            </p>
            """);
    }

    public static string PagamentoEstornado(string nomeAluno, decimal valor, string linkPortal)
    {
        // Formatação pt-BR explícita (R$ 149,90) — independe de culture do processo.
        var valorFormatado = valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
        return Layout(
            "Cobrança estornada",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua cobrança no valor de <strong>R$ {valorFormatado}</strong> foi
              <strong style="color:#2e7d32">estornada</strong>.
            </p>
            <p style="color:#444;line-height:1.6">
              O valor será devolvido em até 10 dias úteis pelo mesmo método de pagamento utilizado.
              Cartões podem demorar até 2 ciclos da fatura para refletir.
            </p>
            <a href="{linkPortal}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver pagamentos
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Em caso de dúvidas, fale com quem te treina.
            </p>
            """);
    }

    public static string PagamentoEmDisputa(string nomeTreinador, string nomeAluno, decimal valor, string motivo)
    {
        // Formatação pt-BR explícita (R$ 149,90).
        var valorFormatado = valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
        var motivoFormatado = MotivoDisputaPtBr(motivo);
        // Stripe Dashboard URL bem-conhecida; URL pública estável da Stripe — única
        // forma do treinador responder à disputa (não temos UI própria).
#pragma warning disable S1075
        const string dashboardStripeUrl = "https://dashboard.stripe.com/disputes";
#pragma warning restore S1075
        return Layout(
            "URGENTE — Disputa de pagamento aberta",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeTreinador)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              <strong style="color:#c62828">Atenção: uma disputa de pagamento (chargeback) foi aberta por {Enc(nomeAluno)}.</strong>
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Aluno</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{Enc(nomeAluno)}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Valor disputado</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">R$ {valorFormatado}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Motivo</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{Enc(motivoFormatado)}</td>
              </tr>
            </table>
            <p style="color:#444;line-height:1.6">
              Você tem <strong>de 7 a 21 dias</strong> para responder à disputa pelo painel do Stripe com evidências
              (provas de entrega de fichas, registros de execução, comunicação com o aluno). Sem resposta, o valor
              é devolvido automaticamente ao cliente e a taxa do chargeback é cobrada.
            </p>
            <p style="color:#444;line-height:1.6">
              A assinatura do aluno foi <strong>congelada</strong> automaticamente — o acesso a recursos
              pagos fica bloqueado até a disputa ser resolvida.
            </p>
            <a href="{dashboardStripeUrl}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#c62828;color:#FFFFFF;text-decoration:none;border-radius:4px;font-weight:bold">
              Responder no Stripe
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Esta resposta acontece pelo painel do Stripe — a forzion.tech não tem interface própria pra essa etapa.
            </p>
            """);
    }

    private static string MotivoDisputaPtBr(string motivo) => motivo switch
    {
        "fraudulent" => "Fraude alegada pelo cliente",
        "duplicate" => "Cobrança duplicada alegada pelo cliente",
        "subscription_canceled" => "Assinatura cancelada alegada pelo cliente",
        "product_not_received" => "Produto/serviço não recebido alegado pelo cliente",
        "product_unacceptable" => "Produto/serviço inadequado alegado pelo cliente",
        "credit_not_processed" => "Crédito não processado alegado pelo cliente",
        "general" => "Disputa geral",
        "unrecognized" => "Cobrança não reconhecida pelo cliente",
        "unknown" or "" => "Motivo não informado pelo Stripe",
        _ => motivo,
    };

    public static string AssinaturaInadimplente(string nomeAluno, int tentativasFalhas, string linkPortal) =>
        Layout(
            "Conta restrita por inadimplência",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua conta forzion.tech está <strong style="color:#c62828">restrita por inadimplência</strong>.
              Regularize seu pagamento para liberar acesso completo a fichas e execuções.
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Tentativas falhas</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{tentativasFalhas}</td>
              </tr>
            </table>
            <a href="{linkPortal}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Regularizar agora
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Em caso de dúvidas, fale com quem te treina.
            </p>
            """);

    public static string AssinaturaAlunoCriada(string nomeAluno, string nomeTreinador, string nomePacote, decimal valor) =>
        Layout(
            "AssinaturaAluno criada!",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua assinatura com <strong>{Enc(nomeTreinador)}</strong> foi criada com sucesso.
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Pacote</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{Enc(nomePacote)}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Valor mensal</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">R$ {valor:N2}</td>
              </tr>
            </table>
            <p style="color:#444;line-height:1.6">
              As cobranças serão geradas mensalmente por quem te treina.
              Você receberá uma notificação quando houver um pagamento pendente.
            </p>
            <a href="https://forzion.tech/aluno/assinatura"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver assinatura
            </a>
            """);

    public static string AssinaturaCancelada(string nomeAluno, DateTime dataCancelamento, string nomeTreinador)
    {
        var ptBr = CultureInfo.GetCultureInfo("pt-BR");
        var dataFormatada = dataCancelamento.ToString("dd/MM/yyyy", ptBr);
        return Layout(
            "Assinatura cancelada",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua assinatura com <strong>{Enc(nomeTreinador)}</strong>
              foi <strong style="color:#c62828">cancelada</strong> em <strong>{dataFormatada}</strong>.
            </p>
            <p style="color:#444;line-height:1.6">
              A partir de agora suas fichas ficam em modo somente leitura e novas execuções
              não poderão ser registradas. Obrigado por ter feito parte da forzion.tech!
            </p>
            <p style="color:#444;line-height:1.6">
              Para reativar, entre em contato com quem te treina.
            </p>
            """);
    }

    public static string NovoAlunoPendente(string nomeTreinador, string nomeAluno) =>
        Layout(
            "Novo aluno aguardando aprovação",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeTreinador)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              <strong>{Enc(nomeAluno)}</strong> solicitou vínculo com você
              e está <strong>aguardando aprovação</strong>.
            </p>
            <p style="color:#444;line-height:1.6">
              Acesse o app para aceitar ou recusar o pedido.
            </p>
            <a href="https://forzion.tech/treinador/alunos"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver pedidos pendentes
            </a>
            """);

    public static string AlunoCancelouAssinatura(string nomeTreinador, string nomeAluno, decimal valor)
    {
        var ptBr = CultureInfo.GetCultureInfo("pt-BR");
        var valorFormatado = valor.ToString("N2", ptBr);
        return Layout(
            "Aluno cancelou assinatura",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeTreinador)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              <strong>{Enc(nomeAluno)}</strong> acabou de cancelar a assinatura
              pelo portal.
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Valor mensal</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">R$ {valorFormatado}</td>
              </tr>
            </table>
            <p style="color:#444;line-height:1.6">
              Novas cobranças não serão geradas. Acesse o portal para confirmar o status e
              avaliar se quer entrar em contato com o aluno.
            </p>
            <a href="https://forzion.tech/treinador/alunos"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver alunos
            </a>
            """);
    }

    public static string MensagemSuporte(
        string nomeRemetente,
        string emailRemetente,
        string tipoConta,
        string categoria,
        string assunto,
        string descricao)
    {
        // Encode ANTES de injetar <br>: texto livre do usuário (assunto/descrição) é PII potencial
        // e vetor de HTML injection no corpo do e-mail. Encode neutraliza tags; só então quebras
        // de linha viram <br> (preserva formatação sem reabrir injeção).
        var descricaoHtml = Enc(descricao).Replace("\n", "<br>", StringComparison.Ordinal);
        return Layout(
            "Nova mensagem de suporte",
            $"""
            <p style="color:#444;line-height:1.6">Mensagem recebida pela página de contato:</p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Nome</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{Enc(nomeRemetente)}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">E-mail</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{Enc(emailRemetente)}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Tipo de conta</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{Enc(tipoConta)}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Categoria</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{Enc(categoria)}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Assunto</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{Enc(assunto)}</td>
              </tr>
            </table>
            <p style="color:#666;font-size:14px;margin:0 0 4px">Descrição</p>
            <p style="color:#444;line-height:1.6;white-space:pre-wrap;background:#F5F5F5;border-radius:4px;padding:16px;margin:0">{descricaoHtml}</p>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Responda diretamente a este e-mail para falar com o usuário (reply-to configurado).
            </p>
            """);
    }

    public static string AssinaturaReativada(string nomeAluno, string linkPortal) =>
        Layout(
            "Assinatura reativada!",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeAluno)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Seu pagamento foi processado com sucesso e sua assinatura está
              <strong style="color:#2e7d32">reativada</strong>.
              Você já tem acesso completo à plataforma novamente.
            </p>
            <a href="{linkPortal}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Acessar plataforma
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Em caso de dúvidas, fale com quem te treina.
            </p>
            """);

    public static string CobrancaPlanoFalhou(string nomeTreinador, decimal valor, int tentativasFalhas, string linkPortal)
    {
        var valorFormatado = valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
        var (titulo, mensagem, corEnfase) = tentativasFalhas switch
        {
            <= 1 => (
                "Cobrança do seu plano não processada",
                "Não conseguimos processar a cobrança do seu plano forzion.tech. Verifique seu método de pagamento.",
                "#ef6c00"),
            2 => (
                "Segunda tentativa de cobrança falhou",
                "A segunda tentativa de cobrar seu plano falhou. Regularize para evitar restrição de acesso.",
                "#ef6c00"),
            _ => (
                "Última tentativa antes da restrição de acesso",
                "Esta é a última tentativa antes de sua conta ser marcada como inadimplente e ter acesso restrito.",
                "#c62828")
        };
        return Layout(
            titulo,
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeTreinador)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              <strong style="color:{corEnfase}">{mensagem}</strong>
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Valor</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">R$ {valorFormatado}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Tentativas</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{tentativasFalhas}</td>
              </tr>
            </table>
            <a href="{linkPortal}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Atualizar pagamento
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Em caso de dúvidas, entre em contato com o suporte forzion.tech.
            </p>
            """);
    }

    public static string PlanoInadimplente(string nomeTreinador, int tentativasFalhas, string linkPortal) =>
        Layout(
            "Acesso restrito por inadimplência",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeTreinador)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Seu acesso ao forzion.tech está <strong style="color:#c62828">restrito por inadimplência</strong>
              no plano. Regularize o pagamento para restaurar o acesso completo.
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Tentativas falhas</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{tentativasFalhas}</td>
              </tr>
            </table>
            <a href="{linkPortal}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Regularizar agora
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Em caso de dúvidas, entre em contato com o suporte forzion.tech.
            </p>
            """);

    public static string NfseEmitida(string nomeTreinador, string numeroNfse, decimal valor, DateTime dataEmissao, string linkNotas)
    {
        var ptBr = CultureInfo.GetCultureInfo("pt-BR");
        var valorFormatado = valor.ToString("N2", ptBr);
        var dataFormatada = dataEmissao.ToString("dd/MM/yyyy", ptBr);
        return Layout(
            "Nota fiscal emitida",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeTreinador)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua nota fiscal de serviço eletrônica (NFS-e) foi
              <strong style="color:#2e7d32">emitida</strong> com sucesso.
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Número da NFS-e</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{Enc(numeroNfse)}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Valor</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">R$ {valorFormatado}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Data de emissão</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{dataFormatada}</td>
              </tr>
            </table>
            <p style="color:#444;line-height:1.6">
              Acesse o portal para visualizar e baixar a DANFSe.
            </p>
            <a href="{linkNotas}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver notas fiscais
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Em caso de dúvidas, entre em contato com o suporte forzion.tech.
            </p>
            """);
    }

    public static string NfseBloqueadaDadosFiscais(string nomeTreinador, string linkDadosFiscais) =>
        Layout(
            "Ação necessária — complete seus dados fiscais",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{Enc(nomeTreinador)}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Uma nota fiscal de serviço eletrônica (NFS-e) <strong style="color:#ef6c00">não pôde ser emitida</strong>
              porque seus dados fiscais ainda não foram preenchidos.
            </p>
            <p style="color:#444;line-height:1.6">
              Complete seus dados fiscais no portal. Assim que forem salvos, a nota será
              <strong>reemitida automaticamente</strong>.
            </p>
            <a href="{linkDadosFiscais}"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Completar dados fiscais
            </a>
            <p style="color:#999;font-size:12px;margin-top:24px">
              Em caso de dúvidas, entre em contato com o suporte forzion.tech.
            </p>
            """);

    public static string RelatorioSaude(HealthReport report)
    {
        var ambiente = Enc(report.Ambiente);
        var capturado = report.CapturadoEm.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

        var corpo = new StringBuilder();
        corpo.Append($"""
            <p style="color:#444;line-height:1.6">
              Ambiente <strong>{ambiente}</strong> — situação geral:
              <strong style="color:{CorStatus(report.StatusGeral)}">{report.StatusGeral}</strong>.
            </p>
            <p style="color:#999;font-size:12px;margin:0 0 16px">Capturado em {capturado}.</p>
            """);

        if (report.Liveness is { } liveness)
            corpo.Append(SecaoLiveness(liveness));
        if (report.Kpis is { } kpis)
            corpo.Append(SecaoKpis(kpis));
        if (report.Entregabilidade is { } entrega)
            corpo.Append(SecaoEntregabilidade(entrega));
        if (report.Erros is { } erros)
            corpo.Append(SecaoErros(erros));
        if (report.Outbox is { } outbox)
            corpo.Append(SecaoOutbox(outbox));

        return Layout($"Relatório de saúde — {ambiente}", corpo.ToString());
    }

    private static string CorStatus(StatusSaude status) => status switch
    {
        StatusSaude.Ok => "#2e7d32",
        StatusSaude.Degradado => "#ef6c00",
        _ => "#c62828"
    };

    private static string Secao(string titulo, string conteudo) => $"""
        <h3 style="margin:24px 0 8px;color:#1A1A1A;font-size:15px">{titulo}</h3>
        {conteudo}
        """;

    private static string Linha(string rotulo, string valor) => $"""
        <tr>
          <td style="padding:6px 16px 6px 0;color:#666;font-size:14px">{rotulo}</td>
          <td style="padding:6px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{valor}</td>
        </tr>
        """;

    private static string Tabela(string linhas) =>
        $"""<table cellpadding="0" cellspacing="0" style="border-collapse:collapse">{linhas}</table>""";

    private static string SimNao(bool valor) => valor ? "Sim" : "Não";

    private static string SecaoLiveness(LivenessSecao s)
    {
        var linhas = new StringBuilder();
        linhas.Append(Linha("Banco acessível", SimNao(s.BancoAcessivel)));
        linhas.Append(Linha("E-mail habilitado", SimNao(s.EmailHabilitado)));
        linhas.Append(Linha("Stripe configurado", SimNao(s.StripeConfigurado)));
        linhas.Append(Linha("WhatsApp configurado", SimNao(s.WhatsAppConfigurado)));
        if (!string.IsNullOrEmpty(s.Versao))
            linhas.Append(Linha("Versão", Enc(s.Versao)));
        if (!string.IsNullOrEmpty(s.Commit))
            linhas.Append(Linha("Commit", Enc(s.Commit)));
        return Secao("Infraestrutura", Tabela(linhas.ToString()));
    }

    private static string SecaoKpis(KpisSecao s)
    {
        var linhas = new StringBuilder();
        linhas.Append(Linha("Treinadores ativos", s.TreinadoresAtivos.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Alunos ativos", s.AlunosAtivos.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Novas contas (24h)", s.NovasContas24h.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Pagamentos pendentes", s.PagamentosPendentes.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Pagamentos falhos", s.PagamentosFalhos.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Assinaturas ativas", s.AssinaturasAtivas.ToString(CultureInfo.InvariantCulture)));
        return Secao("Indicadores", Tabela(linhas.ToString()));
    }

    private static string SecaoEntregabilidade(EntregabilidadeSecao s)
    {
        var linhas = new StringBuilder();
        linhas.Append(Linha("Eventos (24h)", s.Total.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Entregues", s.Entregues.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Bounces", s.Bounces.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Spam", s.Spam.ToString(CultureInfo.InvariantCulture)));
        return Secao("Entregabilidade de e-mail", Tabela(linhas.ToString()));
    }

    private static string SecaoErros(ErrosSecao s)
    {
        if (s.Total == 0)
            return Secao("Erros (24h)", """<p style="color:#2e7d32;line-height:1.6;margin:0">Nenhum erro registrado.</p>""");

        var itens = new StringBuilder();
        foreach (var amostra in s.Amostras)
        {
            var quando = amostra.OcorridoEm.ToString("HH:mm", CultureInfo.InvariantCulture);
            var nivel = Enc(amostra.Nivel);
            var origem = Enc(amostra.Origem);
            var mensagem = Enc(amostra.Mensagem);
            itens.Append($"""
                <li style="margin:0 0 8px;color:#444;font-size:13px;line-height:1.5">
                  <strong style="color:#c62828">{nivel}</strong> {quando} — {origem}<br>{mensagem}
                </li>
                """);
        }

        var conteudo = $"""
            <p style="color:#444;line-height:1.6;margin:0 0 8px">Total: <strong>{s.Total}</strong> (amostras abaixo).</p>
            <ul style="margin:0;padding-left:18px">{itens}</ul>
            """;
        return Secao("Erros (24h)", conteudo);
    }

    private static string SecaoOutbox(OutboxSecao s)
    {
        var linhas = new StringBuilder();
        linhas.Append(Linha("Pendentes", s.Pendente.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Processando", s.Processando.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Concluídos", s.Concluido.ToString(CultureInfo.InvariantCulture)));
        linhas.Append(Linha("Falhou", s.Falhou.ToString(CultureInfo.InvariantCulture)));

        if (s.FalhasAmostras.Count == 0)
            return Secao("Outbox de efeitos", Tabela(linhas.ToString()));

        var itens = new StringBuilder();
        foreach (var amostra in s.FalhasAmostras)
        {
            var tipo = Enc(amostra.Tipo);
            var erro = Enc(amostra.UltimoErro ?? "");
            itens.Append($"""
                <li style="margin:0 0 8px;color:#444;font-size:13px;line-height:1.5">
                  <strong style="color:#c62828">{tipo}</strong> ({amostra.Tentativas} tentativas)<br>{erro}
                </li>
                """);
        }

        var conteudo = $"""
            {Tabela(linhas.ToString())}
            <p style="color:#c62828;line-height:1.6;margin:12px 0 8px">Efeitos em falha terminal:</p>
            <ul style="margin:0;padding-left:18px">{itens}</ul>
            """;
        return Secao("Outbox de efeitos", conteudo);
    }
}
