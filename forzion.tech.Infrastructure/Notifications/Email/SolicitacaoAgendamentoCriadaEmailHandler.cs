using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class SolicitacaoAgendamentoCriadaEmailHandler(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IPacoteRepository pacoteRepository,
    IEmailService emailService,
    ILogger<SolicitacaoAgendamentoCriadaEmailHandler> logger) : IDomainEventHandler<SolicitacaoAgendamentoCriadaEvent>
{
    public async Task HandleAsync(SolicitacaoAgendamentoCriadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("SolicitacaoAgendamentoCriadaEmailHandler: treinador {Id} não encontrado.", domainEvent.TreinadorId);
            return;
        }

        var conta = await contaRepository
            .ObterPorIdAsync(treinador.ContaId, cancellationToken)
            .ConfigureAwait(false);

        var emailDestino = conta?.Email.Value;
        if (emailDestino is null)
        {
            logger.LogWarning("SolicitacaoAgendamentoCriadaEmailHandler: treinador {Id} sem e-mail — ignorado.", treinador.Id);
            return;
        }

        var pacote = await pacoteRepository.ObterPorIdAsync(domainEvent.PacoteId, cancellationToken).ConfigureAwait(false);
        var nomeServico = pacote?.Nome ?? "Serviço";

        var fuso = TimeZoneInfo.FindSystemTimeZoneById(treinador.FusoHorario);
        var dataHoraLocal = TimeZoneInfo.ConvertTimeFromUtc(domainEvent.InicioUtc, fuso);

        await emailService.EnviarAsync(
            emailDestino,
            "Nova solicitação de agendamento — forzion.tech",
            EmailTemplates.NovaSolicitacaoAgendamento(treinador.Nome, nomeServico, dataHoraLocal),
            cancellationToken).ConfigureAwait(false);
    }
}
