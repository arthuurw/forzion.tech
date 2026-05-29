namespace forzion.tech.Application.Interfaces;

/// <summary>
/// Mensagem de template WhatsApp (Meta Cloud API <c>type:template</c>).
/// <paramref name="BodyParameters"/> são as variáveis posicionais do corpo
/// (<c>{{1}}</c>, <c>{{2}}</c>...) na ordem definida no template aprovado no painel Meta.
/// </summary>
public sealed record WhatsAppTemplateMessage(
    string Name,
    IReadOnlyList<string> BodyParameters,
    string LanguageCode = "pt_BR");
