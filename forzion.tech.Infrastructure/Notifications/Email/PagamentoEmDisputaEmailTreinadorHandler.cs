using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

/// <summary>
/// Notifica TREINADOR via e-mail urgente quando uma disputa (chargeback) é aberta
/// sobre um pagamento. Disparado por <see cref="PagamentoEmDisputaEvent"/>.
///
/// <para>
/// Resolução de destinatário: pagamento → assinatura → treinador → conta;
/// <c>conta.Email.Value</c> é o canal (treinador sempre tem conta com e-mail
/// verificado no fluxo de onboarding). Link aponta para o painel do Stripe
/// (<c>dashboard.stripe.com/disputes</c>) — é lá que ele responde à disputa;
/// não temos UI própria pra essa etapa.
/// </para>
///
/// <para>
/// Aluno NÃO é notificado: cliente em disputa já sabe (foi ele que abriu).
/// </para>
/// </summary>
public sealed class PagamentoEmDisputaEmailTreinadorHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IAlunoRepository alunoRepository,
    IEmailService emailService,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<PagamentoEmDisputaEmailTreinadorHandler> logger) : IDomainEventHandler<PagamentoEmDisputaEvent>
{
    public async Task HandleAsync(PagamentoEmDisputaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var assinatura = await assinaturaRepository
            .ObterPorIdAsync(domainEvent.AssinaturaAlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("PagamentoEmDisputaEmailTreinadorHandler: assinatura {Id} não encontrada.", domainEvent.AssinaturaAlunoId);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(assinatura.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.Email) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(assinatura.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("PagamentoEmDisputaEmailTreinadorHandler: treinador {Id} não encontrado.", assinatura.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);
        if (conta is null)
        {
            logger.LogWarning("PagamentoEmDisputaEmailTreinadorHandler: conta {Id} do treinador não encontrada.", treinador.ContaId);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(assinatura.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        var nomeAluno = aluno?.Nome ?? "aluno";

        await emailService.EnviarAsync(
            conta.Email.Value,
            "URGENTE — Disputa de pagamento aberta no Stripe",
            EmailTemplates.PagamentoEmDisputa(treinador.Nome, nomeAluno, domainEvent.Valor, domainEvent.MotivoDisputa),
            cancellationToken).ConfigureAwait(false);
    }
}
