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
}
