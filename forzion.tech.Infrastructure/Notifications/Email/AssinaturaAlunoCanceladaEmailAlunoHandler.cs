using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

/// <summary>
/// Confirma para o aluno via e-mail que sua assinatura foi cancelada. Disparado
/// por <see cref="AssinaturaAlunoCanceladaEvent"/>. Preferência <c>Aluno.Email</c>,
/// fallback <c>Conta.Email</c>.
/// </summary>
public sealed class AssinaturaAlunoCanceladaEmailAlunoHandler(
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    ITreinadorRepository treinadorRepository,
    IEmailService emailService,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<AssinaturaAlunoCanceladaEmailAlunoHandler> logger) : IDomainEventHandler<AssinaturaAlunoCanceladaEvent>
{
    public async Task HandleAsync(AssinaturaAlunoCanceladaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.Email) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("AssinaturaAlunoCanceladaEmailAlunoHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
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
            logger.LogWarning("AssinaturaAlunoCanceladaEmailAlunoHandler: aluno {Id} sem e-mail — ignorado.", aluno.Id);
            return;
        }

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        var nomeTreinador = treinador?.Nome ?? "quem te treina";

        await emailService.EnviarAsync(
            emailDestino,
            "Assinatura cancelada — forzion.tech",
            EmailTemplates.AssinaturaCancelada(aluno.Nome, domainEvent.OcorridoEm, nomeTreinador),
            cancellationToken).ConfigureAwait(false);
    }
}
