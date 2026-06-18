using System.Security.Cryptography;
using System.Text;

namespace forzion.tech.Api.Security;

internal static class AesGcmEnvelope
{
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;

    public static string Cifrar(byte[] chave, string textoPuro)
    {
        var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce);
        var plaintext = Encoding.UTF8.GetBytes(textoPuro);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TamanhoTag];

        using var aes = new AesGcm(chave, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var saida = new byte[TamanhoNonce + TamanhoTag + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, saida, 0, TamanhoNonce);
        Buffer.BlockCopy(tag, 0, saida, TamanhoNonce, TamanhoTag);
        Buffer.BlockCopy(ciphertext, 0, saida, TamanhoNonce + TamanhoTag, ciphertext.Length);
        return Convert.ToBase64String(saida);
    }

    public static string Decifrar(byte[] chave, string textoProtegido)
    {
        var dados = Convert.FromBase64String(textoProtegido);
        if (dados.Length < TamanhoNonce + TamanhoTag)
            throw new CryptographicException("Conteúdo cifrado de DataProtection malformado.");

        var nonce = new byte[TamanhoNonce];
        var tag = new byte[TamanhoTag];
        var ciphertext = new byte[dados.Length - TamanhoNonce - TamanhoTag];
        Buffer.BlockCopy(dados, 0, nonce, 0, TamanhoNonce);
        Buffer.BlockCopy(dados, TamanhoNonce, tag, 0, TamanhoTag);
        Buffer.BlockCopy(dados, TamanhoNonce + TamanhoTag, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(chave, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }
}
