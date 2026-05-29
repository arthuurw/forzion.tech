using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

/// <summary>
/// Notifica o treinador via e-mail que um de seus alunos cancelou a assinatura.
/// Disparado por <see cref="AssinaturaAlunoCanceladaEvent"/>. Resolve treinador
/// → Conta.Email (treinador não tem campo Email próprio como aluno).
/// </summary>
public sealed class AssinaturaAlunoCanceladaEmailTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IAlunoRepository alunoRepository,
    IEmailService emailService,
    ILogger<AssinaturaAlunoCanceladaEmailTreinadorHandler> logger) : IDomainEventHandler<AssinaturaAlunoCanceladaEvent>
{
    public async Task HandleAsync(AssinaturaAlunoCanceladaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("AssinaturaAlunoCanceladaEmailTreinadorHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository.ObterPorIdAsync(treinador.ContaId, cancellationToken).ConfigureAwait(false);
        var emailDestino = conta?.Email.Value;
        if (emailDestino is null)
        {
            logger.LogWarning("AssinaturaAlunoCanceladaEmailTreinadorHandler: treinador {Id} sem e-mail — ignorado.", treinador.Id);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        var nomeAluno = aluno?.Nome ?? "Um aluno";

        await emailService.EnviarAsync(
            emailDestino,
            "Aluno cancelou assinatura — forzion.tech",
            EmailTemplates.AlunoCancelouAssinatura(treinador.Nome, nomeAluno, domainEvent.Valor),
            cancellationToken).ConfigureAwait(false);
    }
}
