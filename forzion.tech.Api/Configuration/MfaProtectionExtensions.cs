using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Api.Configuration;

public static class MfaProtectionExtensions
{
    private const int TamanhoChaveBytes = 32;

    public static IServiceCollection AddMfaProtection(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var chaveBase64 = configuration["Mfa:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(chaveBase64))
            throw new InvalidOperationException("Configuração 'Mfa:EncryptionKey' não encontrada. Configure via User Secrets ou variável de ambiente.");

        byte[] chave;
        try
        {
            chave = Convert.FromBase64String(chaveBase64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("'Mfa:EncryptionKey' deve ser base64 válido de 32 bytes. Gere via: openssl rand -base64 32");
        }

        if (chave.Length != TamanhoChaveBytes)
            throw new InvalidOperationException("'Mfa:EncryptionKey' deve decodificar para exatamente 32 bytes (AES-256). Gere via: openssl rand -base64 32");

        services.AddSingleton<IMfaSecretProtector>(_ => new MfaSecretProtector(chave));
        return services;
    }
}
