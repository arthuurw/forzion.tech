using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Api.Security;

public sealed class AesGcmXmlDecryptor : IXmlDecryptor
{
    private readonly DataProtectionAesGcmKey _chave;

    public AesGcmXmlDecryptor(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _chave = services.GetRequiredService<DataProtectionAesGcmKey>();
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        var cifrado = encryptedElement.Element("value")?.Value
            ?? throw new CryptographicException("Elemento de DataProtection cifrado sem nó 'value'.");
        return XElement.Parse(AesGcmEnvelope.Decifrar(_chave.Chave, cifrado));
    }
}
