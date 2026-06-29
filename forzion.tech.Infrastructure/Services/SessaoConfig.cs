using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace forzion.tech.Infrastructure.Services;

/// <summary>
/// Janelas de sessão por papel. Lê <c>Auth:Sessao:&lt;papel&gt;:*</c> com defaults
/// ancorados em OWORK/OWASP (spec §5, T0 2026-06-13): privilégio alto = janelas curtas.
/// Access cai p/ <c>Auth:JwtExpirationMinutes</c> antes do default p/ não quebrar o ajuste global.
/// </summary>
public static class SessaoConfig
{
    public static int AccessMinutes(IConfiguration config, TipoConta tipo)
    {
        if (int.TryParse(config[$"Auth:Sessao:{tipo}:AccessMinutes"], out var perRole))
            return perRole;
        if (int.TryParse(config["Auth:JwtExpirationMinutes"], out var global))
            return global;
        return tipo == TipoConta.SystemAdmin ? 10 : 15;
    }

    public static TimeSpan IdleWindow(IConfiguration config, TipoConta tipo)
    {
        if (int.TryParse(config[$"Auth:Sessao:{tipo}:RefreshIdleMinutes"], out var perRole))
            return TimeSpan.FromMinutes(perRole);
        return TimeSpan.FromMinutes(tipo == TipoConta.SystemAdmin ? 120 : 10080); // 2h vs 7d
    }

    public static TimeSpan AbsoluteWindow(IConfiguration config, TipoConta tipo)
    {
        if (int.TryParse(config[$"Auth:Sessao:{tipo}:AbsolutoMinutes"], out var perRole))
            return TimeSpan.FromMinutes(perRole);
        return TimeSpan.FromMinutes(tipo switch
        {
            TipoConta.SystemAdmin => 480,   // 8h
            TipoConta.Treinador => 43200,   // 30d
            _ => 129600,                    // 90d (Aluno)
        });
    }
}
