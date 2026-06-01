using System.Security.Cryptography;
using System.Text;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public class EmailVerificationSender(
    IEmailVerificationTokenRepository tokenRepository,
    IEmailService emailService,
    IUnitOfWork unitOfWork,
    IOptions<AppSettings> appSettings,
    TimeProvider timeProvider,
    ILogger<EmailVerificationSender> logger)
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
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (!emailService.Habilitado)
        {
            logger.LogDebug("EmailVerificationSender: e-mail desabilitado — token gerado para conta {ContaId}.", contaId);
            return;
        }

        var verifyLink = $"{appSettings.Value.FrontendBaseUrl}/verify-email?token={rawToken}";

        await emailService.EnviarAsync(
            email,
            "Confirme seu e-mail — forzion.tech",
            EmailTemplates.VerificarEmail(email, verifyLink),
            cancellationToken).ConfigureAwait(false);
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
