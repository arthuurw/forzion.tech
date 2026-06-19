using System.Security.Cryptography;
using System.Text;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Infrastructure.Notifications.Email;

public class EmailVerificationSender(
    IEmailVerificationTokenRepository tokenRepository,
    IEmailCriticoDispatcher emailCritico,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual async Task EnviarAsync(Guid contaId, string email, CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = GenerateRawToken();
        var tokenHash = ComputeHash(rawToken);

        var token = EmailVerificationToken.Criar(
            contaId,
            tokenHash,
            agora.AddHours(24),
            agora).Value;

        await tokenRepository.AdicionarAsync(token, cancellationToken).ConfigureAwait(false);
        emailCritico.Enfileirar(EmailCriticoTemplate.VerificarEmail, email, rawToken);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ComputeHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
