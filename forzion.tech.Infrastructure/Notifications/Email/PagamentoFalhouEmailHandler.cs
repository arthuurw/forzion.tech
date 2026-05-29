using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

/// <summary>
/// IH.4 — notifica aluno via email que uma cobrança falhou. Disparado em toda
/// tentativa (1ª, 2ª, 3ª+). Template tem tom progressivo via
/// <c>TentativasFalhasConsecutivas</c>.
///
/// Resolução de destinatário: assinatura → aluno; preferência <c>Aluno.Email</c>,
/// fallback <c>Conta.Email</c> (mesmo pattern de PagamentoCriadoEmailHandler).
/// Link sempre pro portal forzion (<c>App:FrontendBaseUrl</c>) — nunca Stripe.
/// </summary>
public sealed class PagamentoFalhouEmailHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    ILogger<PagamentoFalhouEmailHandler> logger) : IDomainEventHandler<PagamentoFalhouEvent>
{
    public async Task HandleAsync(PagamentoFalhouEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var assinatura = await assinaturaRepository
            .ObterPorIdAsync(domainEvent.AssinaturaAlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("PagamentoFalhouEmailHandler: assinatura {Id} não encontrada.", domainEvent.AssinaturaAlunoId);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(assinatura.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("PagamentoFalhouEmailHandler: aluno {Id} não encontrado.", assinatura.AlunoId);
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
            logger.LogWarning("PagamentoFalhouEmailHandler: aluno {Id} sem e-mail — ignorado.", aluno.Id);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";

        await emailService.EnviarAsync(
            emailDestino,
            "Cobrança não processada — forzion.tech",
            EmailTemplates.CobrancaFalhou(aluno.Nome, assinatura.Valor, domainEvent.TentativasFalhasConsecutivas, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
