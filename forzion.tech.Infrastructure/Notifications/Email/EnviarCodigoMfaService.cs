using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class EnviarCodigoMfaService(
    IMfaChallengeRepository challengeRepository,
    IEmailBackgroundDispatcher emailBackground,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<EnviarCodigoMfaService> logger) : IEnviarCodigoMfaService
{
    private const int ValidadeMinutos = 10;

    public Task EnviarAsync(Conta conta, MfaProposito proposito, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conta);
        return EnviarCore(conta, proposito, cancellationToken);
    }

    private async Task EnviarCore(Conta conta, MfaProposito proposito, CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var codigo = GerarCodigo();
        var codigoHash = ComputeHash(codigo);

        var challenge = MfaChallenge.Criar(conta.Id, codigoHash, proposito, agora.AddMinutes(ValidadeMinutos), agora).Value;
        await challengeRepository.AdicionarAsync(challenge, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        var destino = conta.Email.Value;
        emailBackground.Disparar((email, ct) =>
        {
            if (email.Habilitado)
                return email.EnviarAsync(destino, "Seu código de verificação — forzion.tech", EmailTemplates.CodigoMfa(codigo), ct);

            logger.LogInformation("MFA: e-mail desabilitado; código {Proposito} da conta {ContaId} = {Codigo}", proposito, conta.Id, codigo);
            return Task.CompletedTask;
        });
    }

    private static string GerarCodigo() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    private static string ComputeHash(string codigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo))).ToLowerInvariant();
}
