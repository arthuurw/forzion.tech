using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

/// <summary>
/// Notifica aluno via email que uma cobrança paga foi estornada (refund manual
/// do treinador via Stripe Dashboard). Disparado por <see cref="PagamentoEstornadoEvent"/>.
///
/// Resolução de destinatário: assinatura → aluno; preferência <c>Aluno.Email</c>,
/// fallback <c>Conta.Email</c> (mesmo pattern de PagamentoCriadoEmailHandler).
/// Link sempre pro portal forzion (<c>App:FrontendBaseUrl</c>) — nunca Stripe.
/// </summary>
public sealed class PagamentoEstornadoEmailHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    ILogger<PagamentoEstornadoEmailHandler> logger) : IDomainEventHandler<PagamentoEstornadoEvent>
{
    public async Task HandleAsync(PagamentoEstornadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var assinatura = await assinaturaRepository
            .ObterPorIdAsync(domainEvent.AssinaturaAlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("PagamentoEstornadoEmailHandler: assinatura {Id} não encontrada.", domainEvent.AssinaturaAlunoId);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(assinatura.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("PagamentoEstornadoEmailHandler: aluno {Id} não encontrado.", assinatura.AlunoId);
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
            logger.LogWarning("PagamentoEstornadoEmailHandler: aluno {Id} sem e-mail — ignorado.", aluno.Id);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";

        await emailService.EnviarAsync(
            emailDestino,
            "Cobrança estornada — forzion.tech",
            EmailTemplates.PagamentoEstornado(aluno.Nome, domainEvent.Valor, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
