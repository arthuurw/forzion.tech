using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

/// <summary>
/// Notifica o treinador via e-mail que um novo aluno está aguardando aprovação de vínculo.
/// Disparado por <see cref="VinculoPendenteCriadoEvent"/>. Resolve treinador
/// → Conta.Email (treinador não tem campo Email próprio).
/// </summary>
public sealed class VinculoPendenteCriadoEmailTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IAlunoRepository alunoRepository,
    IEmailService emailService,
    ILogger<VinculoPendenteCriadoEmailTreinadorHandler> logger) : IDomainEventHandler<VinculoPendenteCriadoEvent>
{
    public async Task HandleAsync(VinculoPendenteCriadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        if (treinador is null)
        {
            logger.LogWarning("VinculoPendenteCriadoEmailTreinadorHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);

        var emailDestino = conta?.Email.Value;
        if (emailDestino is null)
        {
            logger.LogWarning("VinculoPendenteCriadoEmailTreinadorHandler: treinador {Id} sem e-mail — ignorado.", treinador.Id);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        var nomeAluno = aluno?.Nome ?? "Um aluno";

        await emailService.EnviarAsync(
            emailDestino,
            "Novo aluno aguardando aprovação — forzion.tech",
            EmailTemplates.NovoAlunoPendente(treinador.Nome, nomeAluno),
            cancellationToken).ConfigureAwait(false);
    }
}
