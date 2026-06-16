using System.Security.Cryptography;
using System.Text;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public record EsqueceuSenhaCommand(string Email);

public class EsqueceuSenhaHandler(
    IContaRepository contaRepository,
    IPasswordResetTokenRepository tokenRepository,
    IEmailBackgroundDispatcher emailBackground,
    IUnitOfWork unitOfWork,
    IOptions<AppSettings> appSettings,
    TimeProvider timeProvider,
    ILogger<EsqueceuSenhaHandler> logger)
{
    public virtual Task HandleAsync(
        EsqueceuSenhaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task HandleAsyncCore(
        EsqueceuSenhaCommand command,
        CancellationToken cancellationToken)
    {
        var emailNormalizado = command.Email.Trim().ToLowerInvariant();

        var conta = await contaRepository
            .ObterPorEmailAsync(emailNormalizado, cancellationToken)
            .ConfigureAwait(false);

        // Não revelar se o e-mail existe ou não
        if (conta is null)
        {
            logger.LogDebug("EsqueceuSenhaHandler: conta não encontrada para {Email} — ignorado.", emailNormalizado);
            return;
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = GenerateRawToken();
        var tokenHash = ComputeHash(rawToken);

        var resetToken = PasswordResetToken.Criar(
            conta.Id,
            tokenHash,
            agora.AddHours(1),
            agora).Value;

        await tokenRepository.AdicionarAsync(resetToken, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        var destino = conta.Email.Value;
        var resetLink = $"{appSettings.Value.FrontendBaseUrl}/reset-password?token={rawToken}";
        emailBackground.Disparar((email, ct) =>
            email.Habilitado
                ? email.EnviarAsync(destino, "Redefinição de senha — forzion.tech", EmailTemplates.RedefinirSenha(destino, resetLink), ct)
                : Task.CompletedTask);
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
