using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Services;

public sealed class NfseMtlsCertificate : IDisposable
{
    public X509Certificate2 Certificado { get; }

    public NfseMtlsCertificate(IOptions<NfseSettings> settings)
    {
        var s = settings.Value;
        Certificado = new X509Certificate2(s.CertificadoPath, s.CertificadoSenha, X509KeyStorageFlags.EphemeralKeySet);
    }

    public void Dispose() => Certificado.Dispose();
}
