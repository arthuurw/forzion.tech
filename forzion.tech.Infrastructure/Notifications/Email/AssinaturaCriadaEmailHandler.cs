using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class AssinaturaCriadaEmailHandler(
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    IPacoteAlunoRepository pacoteRepository,
    IEmailService emailService,
    ILogger<AssinaturaCriadaEmailHandler> logger) : IDomainEventHandler<AssinaturaCriadaEvent>
{
    public async Task HandleAsync(AssinaturaCriadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (aluno is null)
        {
            logger.LogWarning("AssinaturaCriadaEmailHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (aluno.Email is null)
        {
            logger.LogDebug("AssinaturaCriadaEmailHandler: aluno {Id} sem e-mail cadastrado — ignorado.", domainEvent.AlunoId);
            return;
        }

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        var pacote = await pacoteRepository
            .ObterPorIdAsync(domainEvent.PacoteAlunoId, cancellationToken)
            .ConfigureAwait(false);

        var nomeTreinador = treinador?.Nome ?? "seu treinador";
        var nomePacote = pacote?.Nome ?? "Padrão";

        await emailService.EnviarAsync(
            aluno.Email.Value,
            "Assinatura criada — forzion.tech",
            EmailTemplates.AssinaturaCriada(aluno.Nome, nomeTreinador, nomePacote, domainEvent.Valor),
            cancellationToken).ConfigureAwait(false);
    }
}
