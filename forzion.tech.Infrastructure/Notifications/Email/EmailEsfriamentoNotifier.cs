using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class EmailEsfriamentoNotifier(
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<EmailEsfriamentoNotifier> logger) : IEmailEsfriamentoNotifier
{
    public async Task NotificarAsync(Guid alunoId, TipoNotificacao tipo, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;
        if (tipo is not (TipoNotificacao.LembreteLeve or TipoNotificacao.Recuperacao)) return;

        var aluno = await alunoRepository.ObterPorIdAsync(alunoId, cancellationToken).ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("EmailEsfriamentoNotifier: aluno {Id} não encontrado — ignorado.", alunoId);
            return;
        }

        var conta = await contaRepository.ObterPorIdAsync(aluno.ContaId, cancellationToken).ConfigureAwait(false);

        var emailDestino = aluno.Email?.Value ?? conta?.Email.Value;
        if (emailDestino is null)
        {
            logger.LogWarning("EmailEsfriamentoNotifier: aluno {Id} sem e-mail — ignorado.", alunoId);
            return;
        }

        var canais = await planoNotificationPolicy.ResolverPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);
        if (!canais.Email) return;

        if (conta?.NotificacoesEngajamentoEmailOptOut == true) return;

        var (assunto, corpo) = tipo == TipoNotificacao.LembreteLeve
            ? ("Bora treinar? — forzion.tech", EmailTemplates.LembreteLeve(aluno.Nome))
            : ("Vamos retomar seu treino — forzion.tech", EmailTemplates.Recuperacao(aluno.Nome));

        await emailService.EnviarAsync(emailDestino, assunto, corpo, cancellationToken).ConfigureAwait(false);
    }
}
