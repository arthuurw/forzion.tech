using System.Security.Cryptography;
using System.Xml.Linq;
using FluentAssertions;
using forzion.tech.Api.Security;

namespace forzion.tech.Tests.Api.Security;

public class AesGcmXmlEncryptorTests
{
    private static DataProtectionAesGcmKey NovaChave() =>
        new(RandomNumberGenerator.GetBytes(DataProtectionAesGcmKey.TamanhoChave));

    [Fact]
    public void EncryptDecrypt_RoundTrip_PreservaXml()
    {
        var chave = NovaChave();
        var encryptor = new AesGcmXmlEncryptor(chave);
        var decryptor = new AesGcmXmlDecryptor(chave);
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
        var outroDecryptor = new AesGcmXmlDecryptor(NovaChave());

        var act = () => outroDecryptor.Decrypt(info.EncryptedElement);

        act.Should().Throw<CryptographicException>();
    }
}
