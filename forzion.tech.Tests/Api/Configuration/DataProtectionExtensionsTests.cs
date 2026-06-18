using System.Security.Cryptography;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.Api.Configuration;

public class DataProtectionExtensionsTests
{
    private static IConfiguration CriarConfig(string? chave = null)
    {
        var dict = new Dictionary<string, string?>();
        if (chave is not null) dict["DataProtection:EncryptionKey"] = chave;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void AddDataProtectionPersistence_ChaveAusente_Lanca()
    {
        var act = () => new ServiceCollection().AddDataProtectionPersistence(CriarConfig());
        act.Should().Throw<InvalidOperationException>().WithMessage("*'DataProtection:EncryptionKey'*");
    }

    [Fact]
    public void AddDataProtectionPersistence_ChaveNaoBase64_Lanca()
    {
        var act = () => new ServiceCollection().AddDataProtectionPersistence(CriarConfig("não-é-base64-!!"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*base64*");
    }

    [Fact]
    public void AddDataProtectionPersistence_ChaveTamanhoErrado_Lanca()
    {
        var dezesseis = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var act = () => new ServiceCollection().AddDataProtectionPersistence(CriarConfig(dezesseis));
        act.Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void AddDataProtectionPersistence_ChaveValida_RegistraChaveAesGcm()
    {
        var chave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var services = new ServiceCollection();

        services.AddDataProtectionPersistence(CriarConfig(chave));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<DataProtectionAesGcmKey>().Chave.Should().HaveCount(32);
    }
}
