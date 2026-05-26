namespace forzion.tech.Application.Settings;

public class EmailSettings
{
    public string FromName { get; set; } = "forzion.tech";
    public string FromAddress { get; set; } = "noreply@forzion.tech";

    // Não-prod marca e-mail como teste (prefixo no assunto + banner). Default prod-safe: false.
    public bool MarcarComoTeste { get; set; }
    public string PrefixoAssuntoTeste { get; set; } = string.Empty;

    // CSV. Não-prod redireciona destinatários para esses endereços (guardrail anti-envio a usuários reais).
    public string RedirecionarDestinatariosPara { get; set; } = string.Empty;

    // CSV de domínios. Quando preenchido, destinatários nesses domínios passam direto (sem redirect).
    public string AllowlistDominios { get; set; } = string.Empty;
}
