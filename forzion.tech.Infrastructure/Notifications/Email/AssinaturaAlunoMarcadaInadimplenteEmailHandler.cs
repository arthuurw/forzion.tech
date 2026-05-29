using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

/// <summary>
/// IH.4 — notifica aluno via email que sua assinatura foi marcada como
/// inadimplente (Ativa → Inadimplente). Sempre envia.
///
/// Event traz <c>AlunoId</c> direto, mas precisa do e-mail — busca via
/// <see cref="IAlunoRepository"/>; preferência <c>Aluno.Email</c>, fallback
/// <c>Conta.Email</c>. Link sempre pro portal forzion, nunca Stripe.
/// </summary>
public sealed class AssinaturaAlunoMarcadaInadimplenteEmailHandler(
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<AssinaturaAlunoMarcadaInadimplenteEmailHandler> logger) : IDomainEventHandler<AssinaturaAlunoMarcadaInadimplenteEvent>
{
    public async Task HandleAsync(AssinaturaAlunoMarcadaInadimplenteEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var canais = await planoNotificationPolicy
            .ResolverPorAlunoAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.Email) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("AssinaturaAlunoMarcadaInadimplenteEmailHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        string? emailDestino = aluno.Email?.Value;
        if (emailDestino is null)
        {
            var conta = await contaRepository.ObterPorIdAsync(aluno.ContaId, cancellationToken).ConfigureAwait(false);
            emailDestino = conta?.Email.Value;
        }

        if (emailDestino is null)
        {
            logger.LogWarning("AssinaturaAlunoMarcadaInadimplenteEmailHandler: aluno {Id} sem e-mail — ignorado.", aluno.Id);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";

        await emailService.EnviarAsync(
            emailDestino,
            "Conta restrita por inadimplência — forzion.tech",
            EmailTemplates.AssinaturaInadimplente(aluno.Nome, domainEvent.TentativasFalhasConsecutivas, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
