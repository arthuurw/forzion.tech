using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using forzion.tech.Api.Configuration;
using Microsoft.Extensions.Options;

namespace forzion.tech.Api.Endpoints.Agents.Hmac;

internal enum HmacVerificationResult
{
    Valida,
    AssinaturaInvalida,
    TimestampForaDaJanela,
}

internal sealed class HmacSignatureVerifier(IOptions<AgentsHmacOptions> options, TimeProvider timeProvider)
{
    private const string PrefixoDeVersao = "v1=";
    private const int TamanhoDaAssinaturaEmBytes = 32;
    private const double JanelaEmSegundos = 300;

    internal HmacVerificationResult Verificar(
        string metodo,
        string caminhoComQuery,
        ReadOnlySpan<byte> corpo,
        string? assinatura,
        string? timestamp)
    {
        // Ausência, vazio e lixo não-numérico caem aqui e rejeitam — nada de enforcement
        // guardado por `if (parseou)`, que deixaria header ausente passar sem verificação.
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var timestampUnix))
            return HmacVerificationResult.AssinaturaInvalida;

        if (!TentarLerAssinatura(assinatura, out var assinaturaRecebida))
            return HmacVerificationResult.AssinaturaInvalida;

        var payload = CanonicalPayload.Montar(metodo, caminhoComQuery, corpo, timestampUnix);

        // A assinatura é avaliada ANTES da janela: até conferir, o timestamp é entrada não
        // autenticada, e responder pela janela a quem não tem a chave vaza o relógio do servidor.
        if (!AlgumaChaveConfere(payload, assinaturaRecebida))
            return HmacVerificationResult.AssinaturaInvalida;

        var agoraUnix = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var distanciaEmSegundos = Math.Abs((double)agoraUnix - timestampUnix);

        return distanciaEmSegundos > JanelaEmSegundos
            ? HmacVerificationResult.TimestampForaDaJanela
            : HmacVerificationResult.Valida;
    }

    private static bool TentarLerAssinatura(string? assinatura, out byte[] assinaturaRecebida)
    {
        assinaturaRecebida = [];

        if (string.IsNullOrEmpty(assinatura) || !assinatura.StartsWith(PrefixoDeVersao, StringComparison.Ordinal))
            return false;

        var hex = assinatura[PrefixoDeVersao.Length..];
        if (hex.Length != TamanhoDaAssinaturaEmBytes * 2)
            return false;

        try
        {
            assinaturaRecebida = Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            return false;
        }

        return true;
    }

    private bool AlgumaChaveConfere(string payload, byte[] assinaturaRecebida)
    {
        var bytesDoPayload = Encoding.UTF8.GetBytes(payload);

        return Confere(options.Value.SecretAtual, bytesDoPayload, assinaturaRecebida)
            || Confere(options.Value.SecretAnterior, bytesDoPayload, assinaturaRecebida);
    }

    // FixedTimeEquals RETORNA false em spans de tamanhos diferentes (não lança) — a checagem de
    // comprimento é defesa redundante, não pré-condição. Segredo ausente NUNCA confere: não há
    // bypass por falta de configuração.
    private static bool Confere(string? segredo, byte[] payload, byte[] assinaturaRecebida)
    {
        if (string.IsNullOrEmpty(segredo))
            return false;

        var esperada = HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), payload);

        return esperada.Length == assinaturaRecebida.Length
            && CryptographicOperations.FixedTimeEquals(esperada, assinaturaRecebida);
    }
}
