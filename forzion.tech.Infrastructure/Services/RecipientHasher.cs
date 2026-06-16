using System.Security.Cryptography;
using System.Text;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Services;

public sealed class RecipientHasher(IOptions<DeliveryLogSettings> settings) : IRecipientHasher
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(settings.Value.RecipientHashKey);

    public string Hash(string value) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
