using forzion.tech.Api.Security;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace forzion.tech.Api.Configuration;

public static class DataProtectionExtensions
{
    private const string NomeAplicacao = "forzion.tech";

    public static IServiceCollection AddDataProtectionPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var chaveBase64 = configuration["DataProtection:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(chaveBase64))
            throw new InvalidOperationException("Configuração 'DataProtection:EncryptionKey' não encontrada. Configure via User Secrets ou variável de ambiente.");

        byte[] chave;
        try
        {
            chave = Convert.FromBase64String(chaveBase64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("'DataProtection:EncryptionKey' deve ser base64 válido de 32 bytes. Gere via: openssl rand -base64 32");
        }

        if (chave.Length != DataProtectionAesGcmKey.TamanhoChave)
            throw new InvalidOperationException("'DataProtection:EncryptionKey' deve decodificar para exatamente 32 bytes (AES-256). Gere via: openssl rand -base64 32");

        services.AddSingleton(new DataProtectionAesGcmKey(chave));

        services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>()
            .SetApplicationName(NomeAplicacao);

        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
            new ConfigureOptions<KeyManagementOptions>(opt =>
                opt.XmlEncryptor = new AesGcmXmlEncryptor(sp.GetRequiredService<DataProtectionAesGcmKey>())));

        return services;
    }
}
