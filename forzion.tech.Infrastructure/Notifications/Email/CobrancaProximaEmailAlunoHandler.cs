using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class CobrancaProximaEmailAlunoHandler(
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IPlanoNotificationPolicy planoNotificationPolicy,
    IOptions<AppSettings> appSettings,
    ILogger<CobrancaProximaEmailAlunoHandler> logger) : IDomainEventHandler<CobrancaProximaAlunoEvent>
{
    public async Task HandleAsync(CobrancaProximaAlunoEvent domainEvent, CancellationToken cancellationToken = default)
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
            logger.LogWarning("CobrancaProximaEmailAlunoHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
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
            logger.LogWarning("CobrancaProximaEmailAlunoHandler: aluno {Id} sem e-mail — ignorado.", aluno.Id);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/assinatura";

        await emailService.EnviarAsync(
            emailDestino,
            "Sua assinatura renova em 3 dias — forzion.tech",
            EmailTemplates.CobrancaProxima(aluno.Nome, domainEvent.Valor, domainEvent.DataProximaCobranca, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
