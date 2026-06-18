using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace forzion.tech.Api.Security;

public sealed class AesGcmXmlDecryptor : IXmlDecryptor
{
    private readonly DataProtectionAesGcmKey _chave;

    public AesGcmXmlDecryptor(DataProtectionAesGcmKey chave)
    {
        ArgumentNullException.ThrowIfNull(chave);
        _chave = chave;
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        var cifrado = encryptedElement.Element("value")?.Value
            ?? throw new CryptographicException("Elemento de DataProtection cifrado sem nó 'value'.");
        return XElement.Parse(AesGcmEnvelope.Decifrar(_chave.Chave, cifrado));
    }
}
