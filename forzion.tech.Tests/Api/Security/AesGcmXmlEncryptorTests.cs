using System.Security.Cryptography;
using System.Xml.Linq;
using FluentAssertions;
using forzion.tech.Api.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Api.Security;

public class AesGcmXmlEncryptorTests
{
    private static DataProtectionAesGcmKey NovaChave() =>
        new(RandomNumberGenerator.GetBytes(DataProtectionAesGcmKey.TamanhoChave));

    private static AesGcmXmlDecryptor Decryptor(DataProtectionAesGcmKey chave) =>
        new(new ServiceCollection().AddSingleton(chave).BuildServiceProvider());

    [Fact]
    public void EncryptDecrypt_RoundTrip_PreservaXml()
    {
        var chave = NovaChave();
        var encryptor = new AesGcmXmlEncryptor(chave);
        var decryptor = Decryptor(chave);
        var original = new XElement("key", new XAttribute("id", "abc"), new XElement("descriptor", "segredo"));

        var info = encryptor.Encrypt(original);
        var revelado = decryptor.Decrypt(info.EncryptedElement);

        XNode.DeepEquals(revelado, original).Should().BeTrue();
    }

    [Fact]
    public void Encrypt_NaoVazaTextoPuroNoElemento()
    {
        var encryptor = new AesGcmXmlEncryptor(NovaChave());
        var original = new XElement("key", new XElement("descriptor", "segredo-sensivel"));

        var info = encryptor.Encrypt(original);

        info.EncryptedElement.ToString().Should().NotContain("segredo-sensivel");
        info.DecryptorType.Should().Be<AesGcmXmlDecryptor>();
    }

    [Fact]
    public void Decrypt_ChaveDiferente_Lanca()
    {
        var info = new AesGcmXmlEncryptor(NovaChave())
            .Encrypt(new XElement("key", new XElement("descriptor", "segredo")));
        var outroDecryptor = Decryptor(NovaChave());

        var act = () => outroDecryptor.Decrypt(info.EncryptedElement);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DataProtection_AposReinicio_DecifraChavePersistidaViaActivator()
    {
        var dir = Directory.CreateTempSubdirectory("dp-rt");
        try
        {
            var material = RandomNumberGenerator.GetBytes(DataProtectionAesGcmKey.TamanhoChave);

            string protegido;
            using (var primeiro = Provider(dir.FullName, material))
                protegido = primeiro.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("teste").Protect("segredo");

            using var segundo = Provider(dir.FullName, material);
            var aberto = segundo.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("teste").Unprotect(protegido);

            aberto.Should().Be("segredo");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static ServiceProvider Provider(string dir, byte[] material)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new DataProtectionAesGcmKey(material));
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dir))
            .SetApplicationName("forzion.tech");
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
            new ConfigureOptions<KeyManagementOptions>(opt =>
                opt.XmlEncryptor = new AesGcmXmlEncryptor(sp.GetRequiredService<DataProtectionAesGcmKey>())));
        return services.BuildServiceProvider();
    }
}
