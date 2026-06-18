using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace forzion.tech.Api.Security;

public sealed class AesGcmXmlEncryptor : IXmlEncryptor
{
    private readonly DataProtectionAesGcmKey _chave;

    public AesGcmXmlEncryptor(DataProtectionAesGcmKey chave)
    {
        ArgumentNullException.ThrowIfNull(chave);
        _chave = chave;
    }

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var cifrado = AesGcmEnvelope.Cifrar(_chave.Chave, plaintextElement.ToString(SaveOptions.DisableFormatting));
        var elemento = new XElement("encryptedKey", new XElement("value", cifrado));
        return new EncryptedXmlInfo(elemento, typeof(AesGcmXmlDecryptor));
    }
}
