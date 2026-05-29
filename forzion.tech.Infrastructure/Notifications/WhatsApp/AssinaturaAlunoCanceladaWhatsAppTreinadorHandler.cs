using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Notifica o treinador via WhatsApp que um aluno cancelou a assinatura.
/// Disparado por <see cref="AssinaturaAlunoCanceladaEvent"/>.
/// O handler aluno (<see cref="AssinaturaAlunoCanceladaWhatsAppAlunoHandler"/>) cobre a
/// confirmação ao aluno; este cobre a comunicação de perda de receita ao treinador.
/// </summary>
public sealed class AssinaturaAlunoCanceladaWhatsAppTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    ILogger<AssinaturaAlunoCanceladaWhatsAppTreinadorHandler> logger) : IDomainEventHandler<AssinaturaAlunoCanceladaEvent>
{
    public async Task HandleAsync(AssinaturaAlunoCanceladaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("AssinaturaAlunoCanceladaWhatsAppTreinadorHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        if (string.IsNullOrWhiteSpace(treinador.Telefone))
        {
            logger.LogDebug("AssinaturaAlunoCanceladaWhatsAppTreinadorHandler: treinador {Id} sem telefone — ignorado.", treinador.Id);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        await whatsAppNotifier
            .SendTemplateAsync(
                treinador.Telefone,
                WhatsAppTemplates.AlunoCancelouAssinatura(treinador.Nome, aluno?.Nome ?? "Um aluno", domainEvent.Valor),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
