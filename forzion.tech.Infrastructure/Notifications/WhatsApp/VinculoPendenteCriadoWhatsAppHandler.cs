using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Notifica o treinador via WhatsApp que um novo aluno está aguardando aprovação de vínculo.
/// Disparado por <see cref="VinculoPendenteCriadoEvent"/>.
/// </summary>
public sealed class VinculoPendenteCriadoWhatsAppHandler(
    ITreinadorRepository treinadorRepository,
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    ILogger<VinculoPendenteCriadoWhatsAppHandler> logger) : IDomainEventHandler<VinculoPendenteCriadoEvent>
{
    public async Task HandleAsync(VinculoPendenteCriadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("VinculoPendenteCriadoWhatsAppHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        if (string.IsNullOrWhiteSpace(treinador.Telefone))
        {
            logger.LogDebug("VinculoPendenteCriadoWhatsAppHandler: treinador {Id} sem telefone — ignorado.", treinador.Id);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        await whatsAppNotifier
            .SendTemplateAsync(
                treinador.Telefone,
                WhatsAppTemplates.NovoAlunoPendente(treinador.Nome, aluno?.Nome ?? "Um aluno"),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
