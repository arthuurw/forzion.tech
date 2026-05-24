using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class AssinaturaAlunoCriadaEmailHandler(
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    IPacoteRepository pacoteRepository,
    IEmailService emailService,
    ILogger<AssinaturaAlunoCriadaEmailHandler> logger) : IDomainEventHandler<AssinaturaAlunoCriadaEvent>
{
    public async Task HandleAsync(AssinaturaAlunoCriadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (aluno is null)
        {
            logger.LogWarning("AssinaturaAlunoCriadaEmailHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (aluno.Email is null)
        {
            logger.LogDebug("AssinaturaAlunoCriadaEmailHandler: aluno {Id} sem e-mail cadastrado — ignorado.", domainEvent.AlunoId);
            return;
        }

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        var pacote = await pacoteRepository
            .ObterPorIdAsync(domainEvent.PacoteId, cancellationToken)
            .ConfigureAwait(false);

        var nomeTreinador = treinador?.Nome ?? "seu treinador";
        var nomePacote = pacote?.Nome ?? "Padrão";

        await emailService.EnviarAsync(
            aluno.Email.Value,
            "AssinaturaAluno criada — forzion.tech",
            EmailTemplates.AssinaturaAlunoCriada(aluno.Nome, nomeTreinador, nomePacote, domainEvent.Valor),
            cancellationToken).ConfigureAwait(false);
    }
}
