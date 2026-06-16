using System.Security.Cryptography;
using System.Text;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Services;

public sealed class RecipientHasher(IOptions<DeliveryLogSettings> settings) : IRecipientHasher
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(settings.Value.RecipientHashKey);

    public string HashEmail(string email) => Hash(email.Trim().ToLowerInvariant());

    public string HashTelefone(string telefone) =>
        Hash(PhoneNumberNormalizer.Normalizar(telefone) ?? telefone);

    private string Hash(string value) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
