using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Infrastructure.Common;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public record ReenviarVerificacaoCommand(string Email);

public class ReenviarVerificacaoHandler(
    IContaRepository contaRepository,
    EmailVerificationSender sender,
    ILogger<ReenviarVerificacaoHandler> logger)
{
    public virtual Task HandleAsync(
        ReenviarVerificacaoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task HandleAsyncCore(
        ReenviarVerificacaoCommand command,
        CancellationToken cancellationToken)
    {
        var emailNormalizado = command.Email.Trim().ToLowerInvariant();

        var conta = await contaRepository
            .ObterPorEmailAsync(emailNormalizado, cancellationToken)
            .ConfigureAwait(false);

        // Não revelar se o e-mail existe ou se já está verificado
        if (conta is null)
        {
            logger.LogDebug("ReenviarVerificacaoHandler: conta não encontrada para {Email} — ignorado.", MascaraPii.Email(emailNormalizado));
            return;
        }

        if (conta.EmailVerificado)
        {
            logger.LogDebug("ReenviarVerificacaoHandler: conta {ContaId} já verificada — ignorado.", conta.Id);
            return;
        }

        await sender.EnviarAsync(conta.Id, conta.Email.Value, cancellationToken).ConfigureAwait(false);
    }
}
