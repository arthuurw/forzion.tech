using System.Security.Cryptography;
using System.Text;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public class RefreshTokenService(
    IRefreshTokenRepository tokenRepository,
    IRefreshTokenFamilyRepository familyRepository,
    IContaRepository contaRepository,
    IConfiguration configuration,
    ILogger<RefreshTokenService> logger) : IRefreshTokenService
{
    public async Task<RefreshEmitido> EmitirNovaFamiliaAsync(Conta conta, DateTime agora, string? rotulo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conta);

        var absoluto = agora + SessaoConfig.AbsoluteWindow(configuration, conta.TipoConta);
        var familia = RefreshTokenFamily.Criar(conta.Id, absoluto, agora, Truncar(rotulo)).Value;
        await familyRepository.AdicionarAsync(familia, cancellationToken).ConfigureAwait(false);

        var (raw, _) = await CriarTokenAsync(familia, conta.TipoConta, agora, cancellationToken).ConfigureAwait(false);
        return new RefreshEmitido(raw, familia.Id);
    }

    public async Task<RotacaoResultado> RotacionarAsync(string refreshRaw, DateTime agora, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshRaw))
            return RotacaoResultado.Invalido();

        var token = await tokenRepository.BuscarPorHashAsync(Hash(refreshRaw), cancellationToken).ConfigureAwait(false);
        if (token is null)
            return RotacaoResultado.Invalido();

        var familia = await familyRepository.ObterPorIdAsync(token.FamiliaId, cancellationToken).ConfigureAwait(false);
        if (familia is null || !familia.EstaAtiva(agora))
            return RotacaoResultado.Invalido();

        // Token já usado numa família ainda ativa = refresh roubado e reapresentado (NR-3):
        // revoga a cadeia inteira e sinaliza p/ alerta. Este é o ganho de segurança do modelo.
        if (token.UsadoEm.HasValue)
        {
            familia.Revogar(MotivoRevogacaoFamilia.ReuseDetectado, agora);
            logger.LogWarning("Reuse de refresh token detectado — família {FamiliaId} revogada (conta {ContaId}).", familia.Id, familia.ContaId);
            return RotacaoResultado.Reuse(familia.Id);
        }

        if (!token.EstaValido(agora))
            return RotacaoResultado.Invalido();

        var conta = await contaRepository.ObterPorIdAsync(familia.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
            return RotacaoResultado.Invalido();

        var (raw, sucessor) = await CriarTokenAsync(familia, conta.TipoConta, agora, cancellationToken).ConfigureAwait(false);
        token.MarcarUsado(agora, sucessor.Id);

        return RotacaoResultado.Sucesso(conta, familia.Id, raw);
    }

    public async Task RevogarFamiliaAsync(Guid familiaId, MotivoRevogacaoFamilia motivo, DateTime agora, CancellationToken cancellationToken = default)
    {
        var familia = await familyRepository.ObterPorIdAsync(familiaId, cancellationToken).ConfigureAwait(false);
        familia?.Revogar(motivo, agora);
    }

    public async Task RevogarTodasPorContaAsync(Guid contaId, MotivoRevogacaoFamilia motivo, DateTime agora, CancellationToken cancellationToken = default)
    {
        var familias = await familyRepository.ListarAtivasPorContaAsync(contaId, agora, cancellationToken).ConfigureAwait(false);
        foreach (var familia in familias)
            familia.Revogar(motivo, agora);
    }

    private async Task<(string Raw, RefreshToken Token)> CriarTokenAsync(RefreshTokenFamily familia, TipoConta tipo, DateTime agora, CancellationToken cancellationToken)
    {
        var raw = GerarRaw();
        var idle = agora + SessaoConfig.IdleWindow(configuration, tipo);
        // Idle nunca ultrapassa o teto absoluto da família (NR-5: idle ≠ eterno).
        var expiraEm = idle < familia.AbsolutoExpiraEm ? idle : familia.AbsolutoExpiraEm;

        var token = RefreshToken.Criar(familia.Id, Hash(raw), expiraEm, agora).Value;
        await tokenRepository.AdicionarAsync(token, cancellationToken).ConfigureAwait(false);
        return (raw, token);
    }

    private static string GerarRaw() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    private static string? Truncar(string? rotulo) =>
        rotulo is { Length: > 256 } ? rotulo[..256] : rotulo;
}
