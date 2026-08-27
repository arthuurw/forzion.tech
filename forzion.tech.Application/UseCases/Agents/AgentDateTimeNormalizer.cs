namespace forzion.tech.Application.UseCases.Agents;

public static class AgentDateTimeNormalizer
{
    public static DateTime ParaUtcClampado(DateTime? valor, DateTime agora)
    {
        if (valor is not { } bruto)
            return agora;

        var utc = bruto.Kind switch
        {
            DateTimeKind.Utc => bruto,
            DateTimeKind.Local => bruto.ToUniversalTime(),
            _ => DateTime.SpecifyKind(bruto, DateTimeKind.Utc),
        };

        return utc > agora ? agora : utc;
    }
}
