using System.Security.Cryptography;
using System.Text;
using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public sealed class HibpPwnedPasswordsService(HttpClient httpClient, ILogger<HibpPwnedPasswordsService> logger) : IPwnedPasswordsService
{
    public async Task<bool> EstaComprometidaAsync(string senha, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(senha)));
        var prefixo = hash[..5];
        var sufixo = hash[5..];

        try
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Get, $"range/{prefixo}");
            requisicao.Headers.Add("Add-Padding", "true");

            using var resposta = await httpClient.SendAsync(requisicao, cancellationToken).ConfigureAwait(false);
            if (!resposta.IsSuccessStatusCode)
            {
                logger.LogWarning("HIBP retornou {Status}; check de senha vazada fail-open.", (int)resposta.StatusCode);
                return false;
            }

            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ContemSufixoComprometido(corpo, sufixo);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
            && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha ao consultar HIBP; check de senha vazada fail-open.");
            return false;
        }
    }

    private static bool ContemSufixoComprometido(string corpo, string sufixo)
    {
        foreach (var linha in corpo.AsSpan().EnumerateLines())
        {
            var separador = linha.IndexOf(':');
            if (separador <= 0)
                continue;

            var candidato = linha[..separador];
            if (!candidato.Equals(sufixo, StringComparison.OrdinalIgnoreCase))
                continue;

            return int.TryParse(linha[(separador + 1)..], out var ocorrencias) && ocorrencias > 0;
        }

        return false;
    }
}
