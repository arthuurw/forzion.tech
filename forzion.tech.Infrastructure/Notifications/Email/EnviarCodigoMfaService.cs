using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class EnviarCodigoMfaService(
    IMfaChallengeRepository challengeRepository,
    IEmailCriticoDispatcher emailCritico,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IEnviarCodigoMfaService
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
        emailCritico.Enfileirar(EmailCriticoTemplate.CodigoMfa, conta.Email.Value, codigo);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GerarCodigo() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    private static string ComputeHash(string codigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo))).ToLowerInvariant();
}
