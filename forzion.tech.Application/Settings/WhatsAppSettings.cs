namespace forzion.tech.Application.Settings;

/// <summary>
/// Guardrail de ambiente para WhatsApp (análogo a <see cref="EmailSettings"/>).
/// Defaults prod-safe (passthrough). Em não-prod, <see cref="MarcarComoTeste"/>=true
/// ativa o redirect/allowlist de telefone via <c>EnvironmentWhatsAppDecorator</c>.
/// Lido da seção de config "WhatsApp".
/// </summary>
public class WhatsAppSettings
{
    public bool MarcarComoTeste { get; set; }

    /// <summary>CSV de telefones (E.164). Em não-prod, redireciona o destinatário p/ o 1º alvo.</summary>
    public string RedirecionarDestinatariosPara { get; set; } = string.Empty;

    /// <summary>CSV de telefones (E.164) isentos de redirect (passthrough mesmo em não-prod).</summary>
    public string AllowlistTelefones { get; set; } = string.Empty;
}
