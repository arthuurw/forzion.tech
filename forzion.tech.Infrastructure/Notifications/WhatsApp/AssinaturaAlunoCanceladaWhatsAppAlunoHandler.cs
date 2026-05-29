using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Confirma para o aluno via WhatsApp que sua assinatura foi cancelada.
/// Disparado por <see cref="AssinaturaAlunoCanceladaEvent"/>. Sem telefone do
/// aluno, no-op (e-mail handler cobre o canal de confirmação).
///
/// Treinador não recebe WhatsApp aqui — comunicação com treinador segue por
/// e-mail (canal preferido para perda de receita / decisões financeiras).
/// </summary>
public sealed class AssinaturaAlunoCanceladaWhatsAppAlunoHandler(
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IOptions<AppSettings> appSettings,
    ILogger<AssinaturaAlunoCanceladaWhatsAppAlunoHandler> logger) : IDomainEventHandler<AssinaturaAlunoCanceladaEvent>
{
    public async Task HandleAsync(AssinaturaAlunoCanceladaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("AssinaturaAlunoCanceladaWhatsAppAlunoHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("AssinaturaAlunoCanceladaWhatsAppAlunoHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/aluno/pagamentos";

        var mensagem =
            $"Olá, {aluno.Nome}! Sua assinatura forzion.tech foi cancelada conforme solicitado.\n" +
            $"Para reativar, fale com seu treinador.\n\n" +
            $"Histórico: {linkPortal}";

        await whatsAppNotifier
            .SendAsync(aluno.Telefone, mensagem, cancellationToken)
            .ConfigureAwait(false);
    }
}
