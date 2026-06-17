using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class AssinaturaAlunoCriadaEmailHandler(
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    ITreinadorRepository treinadorRepository,
    IPacoteRepository pacoteRepository,
    IEmailService emailService,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<AssinaturaAlunoCriadaEmailHandler> logger) : IDomainEventHandler<AssinaturaAlunoCriadaEvent>
{
    public async Task HandleAsync(AssinaturaAlunoCriadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
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
            logger.LogWarning("AssinaturaAlunoCriadaEmailHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
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
            logger.LogWarning("AssinaturaAlunoCriadaEmailHandler: aluno {Id} sem e-mail — ignorado.", domainEvent.AlunoId);
            return;
        }

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        var pacote = await pacoteRepository
            .ObterPorIdAsync(domainEvent.PacoteId, cancellationToken)
            .ConfigureAwait(false);

        var nomeTreinador = treinador?.Nome ?? "quem te treina";
        var nomePacote = pacote?.Nome ?? "Padrão";

        await emailService.EnviarAsync(
            emailDestino,
            "AssinaturaAluno criada — forzion.tech",
            EmailTemplates.AssinaturaAlunoCriada(aluno.Nome, nomeTreinador, nomePacote, domainEvent.Valor),
            cancellationToken).ConfigureAwait(false);
    }
}
