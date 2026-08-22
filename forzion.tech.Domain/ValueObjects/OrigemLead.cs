using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.ValueObjects;

public sealed record OrigemLead
{
    public string? UserAgent { get; }
    public string? Assistente { get; }

    private OrigemLead(string? userAgent, string? assistente)
    {
        UserAgent = userAgent;
        Assistente = assistente;
    }

    public static Result<OrigemLead?> Criar(string? userAgent, string? assistente)
    {
        var userAgentNormalizado = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
        var assistenteNormalizado = string.IsNullOrWhiteSpace(assistente) ? null : assistente.Trim();

        if (userAgentNormalizado is null && assistenteNormalizado is null)
            return Result.Success<OrigemLead?>(null);

        if (userAgentNormalizado is not null && userAgentNormalizado.Length > 500)
            return Result.Failure<OrigemLead?>(OrigemLeadErrors.UserAgentMuitoLongo);

        if (assistenteNormalizado is not null && assistenteNormalizado.Length > 100)
            return Result.Failure<OrigemLead?>(OrigemLeadErrors.AssistenteMuitoLongo);

        return Result.Success<OrigemLead?>(new OrigemLead(userAgentNormalizado, assistenteNormalizado));
    }
}
