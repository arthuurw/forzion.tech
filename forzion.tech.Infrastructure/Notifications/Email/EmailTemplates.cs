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

    public static string TreinadorAprovado(string nome) =>
        Layout(
            "Conta aprovada!",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{nome}</strong>!</p>
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
            <p style="color:#444;line-height:1.6">Olá, <strong>{nome}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Infelizmente seu cadastro de treinador <strong style="color:#c62828">não foi aprovado</strong>
              neste momento. Entre em contato com nosso suporte para mais informações.
            </p>
            """);

    public static string TreinadorInativado(string nome) =>
        Layout(
            "Conta inativada",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{nome}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua conta de treinador foi <strong style="color:#c62828">inativada</strong>.
              Caso acredite que isso foi um engano, entre em contato com nosso suporte.
            </p>
            """);

    public static string VinculoAprovado(string nomeAluno, string nomeTreinador) =>
        Layout(
            "Vínculo aprovado!",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{nomeAluno}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              O treinador <strong>{nomeTreinador}</strong> aprovou seu vínculo.
              Você já pode acessar suas fichas de treino na plataforma.
            </p>
            <a href="https://forzion.tech/login"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver fichas
            </a>
            """);

    public static string BemVindoAluno(string nome) =>
        Layout(
            "Bem-vindo à forzion.tech!",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{nome}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Seu cadastro foi realizado com sucesso. Aguarde a aprovação do seu treinador
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
            <p style="color:#444;line-height:1.6">Olá, <strong>{nome}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua conta foi <strong style="color:#c62828">inativada</strong>.
              Caso acredite que isso foi um engano, entre em contato com seu treinador.
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
            <p style="color:#444;line-height:1.6">Olá, <strong>{WebUtility.HtmlEncode(nomeAluno)}</strong>!</p>
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
              Em caso de dúvidas, fale com o seu treinador.
            </p>
            """);
    }

    public static string AssinaturaAlunoCriada(string nomeAluno, string nomeTreinador, string nomePacote, decimal valor) =>
        Layout(
            "AssinaturaAluno criada!",
            $"""
            <p style="color:#444;line-height:1.6">Olá, <strong>{nomeAluno}</strong>!</p>
            <p style="color:#444;line-height:1.6">
              Sua assinatura com o treinador <strong>{nomeTreinador}</strong> foi criada com sucesso.
            </p>
            <table cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse">
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Pacote</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">{nomePacote}</td>
              </tr>
              <tr>
                <td style="padding:8px 16px 8px 0;color:#666;font-size:14px">Valor mensal</td>
                <td style="padding:8px 0;color:#1A1A1A;font-weight:bold;font-size:14px">R$ {valor:N2}</td>
              </tr>
            </table>
            <p style="color:#444;line-height:1.6">
              As cobranças serão geradas mensalmente pelo seu treinador.
              Você receberá uma notificação quando houver um pagamento pendente.
            </p>
            <a href="https://forzion.tech/aluno/assinatura"
               style="display:inline-block;margin-top:16px;padding:12px 24px;background:#F5C400;color:#1A1A1A;text-decoration:none;border-radius:4px;font-weight:bold">
              Ver assinatura
            </a>
            """);

    public static string RelatorioSaude(HealthReport report)
    {
        var ambiente = WebUtility.HtmlEncode(report.Ambiente);
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
            linhas.Append(Linha("Versão", WebUtility.HtmlEncode(s.Versao)));
        if (!string.IsNullOrEmpty(s.Commit))
            linhas.Append(Linha("Commit", WebUtility.HtmlEncode(s.Commit)));
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
            var nivel = WebUtility.HtmlEncode(amostra.Nivel);
            var origem = WebUtility.HtmlEncode(amostra.Origem);
            var mensagem = WebUtility.HtmlEncode(amostra.Mensagem);
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
}
