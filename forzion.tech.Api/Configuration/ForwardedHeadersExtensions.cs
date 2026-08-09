using Microsoft.AspNetCore.HttpOverrides;

namespace forzion.tech.Api.Configuration;

public static class ForwardedHeadersExtensions
{
    private const string ChaveKnownNetworks = "ForwardedHeaders:KnownNetworks";

    public static ForwardedHeadersOptions BuildForwardedHeadersOptions(
        this IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var redes = (configuration[ChaveKnownNetworks] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Lista vazia deixa o middleware aceitar X-Forwarded-For de qualquer peer, o que devolve o
        // rate-limit ao balde global sem nenhum sinal. Falhar o boot é o único jeito de o deploy notar.
        if (redes.Length == 0 && (environment.IsProduction() || environment.IsEnvironment("Homolog")))
            throw new InvalidOperationException(
                $"Configuração '{ChaveKnownNetworks}' não encontrada. Configure via variável de ambiente " +
                "ForwardedHeaders__KnownNetworks com os CIDRs dos proxies confiáveis, separados por " +
                "vírgula (ex.: 172.18.0.0/16). Obtenha a subnet com: docker network inspect <rede>.");

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
        };

        foreach (var rede in redes)
        {
            if (!System.Net.IPNetwork.TryParse(rede, out var parsed))
                throw new InvalidOperationException(
                    $"Entrada inválida em '{ChaveKnownNetworks}': '{rede}'. Use notação CIDR " +
                    "com a base da rede (ex.: 172.18.0.0/16).");

            options.KnownIPNetworks.Add(parsed);
        }

        return options;
    }
}
