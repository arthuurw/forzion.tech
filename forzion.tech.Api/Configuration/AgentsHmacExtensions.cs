using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Api.Configuration;

internal sealed class AgentsHmacOptions
{
    public string? SecretAtual { get; set; }

    public string? SecretAnterior { get; set; }
}

internal static class AgentsHmacExtensions
{
    private const string ChaveAtual = "Agents:Hmac:SecretAtual";
    private const int TamanhoMinimoBytes = 32;

    public static IServiceCollection AddAgentsHmac(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.Configure<AgentsHmacOptions>(configuration.GetSection("Agents:Hmac"));

        // Fora de Production/Homolog o boot segue sem segredo — mas o grupo de agentes
        // continua fechado (toda requisição responde 401), nunca aberto.
        if (!environment.IsProduction() && !environment.IsEnvironment("Homolog"))
            return services;

        var secretAtual = configuration[ChaveAtual];

        if (string.IsNullOrWhiteSpace(secretAtual))
            throw new InvalidOperationException($"Configuração '{ChaveAtual}' não encontrada. Configure via variável de ambiente. Gere via: openssl rand -base64 64");

        if (Encoding.UTF8.GetByteCount(secretAtual) < TamanhoMinimoBytes)
            throw new InvalidOperationException($"'{ChaveAtual}' deve ter no mínimo {TamanhoMinimoBytes} bytes. Gere via: openssl rand -base64 64");

        return services;
    }
}
