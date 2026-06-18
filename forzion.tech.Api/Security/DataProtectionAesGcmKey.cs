namespace forzion.tech.Api.Security;

public sealed class DataProtectionAesGcmKey
{
    public const int TamanhoChave = 32;

    public DataProtectionAesGcmKey(byte[] chave)
    {
        ArgumentNullException.ThrowIfNull(chave);
        if (chave.Length != TamanhoChave)
            throw new ArgumentException($"A chave de DataProtection deve ter {TamanhoChave} bytes (AES-256).", nameof(chave));

        Chave = chave;
    }

    public byte[] Chave { get; }
}
