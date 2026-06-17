using System.Security.Cryptography;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.Api.Configuration;

public class MfaProtectionExtensionsTests
{
    private static IConfiguration CriarConfig(string? chave = null)
    {
        var dict = new Dictionary<string, string?>();
        if (chave is not null) dict["Mfa:EncryptionKey"] = chave;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void AddMfaProtection_ChaveAusente_Lanca()
    {
        var act = () => new ServiceCollection().AddMfaProtection(CriarConfig());
        act.Should().Throw<InvalidOperationException>().WithMessage("*'Mfa:EncryptionKey'*");
    }

    [Fact]
    public void AddMfaProtection_ChaveNaoBase64_Lanca()
    {
        var act = () => new ServiceCollection().AddMfaProtection(CriarConfig("não-é-base64-!!"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*base64*");
    }

    [Fact]
    public void AddMfaProtection_ChaveTamanhoErrado_Lanca()
    {
        var dezesseis = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var act = () => new ServiceCollection().AddMfaProtection(CriarConfig(dezesseis));
        act.Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void AddMfaProtection_ChaveValida_RegistraProtectorFuncional()
    {
        var chave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var services = new ServiceCollection();

        services.AddMfaProtection(CriarConfig(chave));

        using var provider = services.BuildServiceProvider();
        var protector = provider.GetRequiredService<IMfaSecretProtector>();
        protector.Revelar(protector.Proteger("JBSWY3DPEHPK3PXP")).Should().Be("JBSWY3DPEHPK3PXP");
    }
}
