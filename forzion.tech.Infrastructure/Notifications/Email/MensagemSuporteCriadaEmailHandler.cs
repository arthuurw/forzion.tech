using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

// Handler DURÁVEL (roda no worker do outbox com retry — registrado em OutboxDurabilityRegistry).
// Resolve nome/e-mail/tipo do remetente live por ContaId: o evento não carrega PII e o worker
// não tem UserContext (sem request HTTP). E-mail vai ao suporte com reply-to = e-mail do usuário.
public sealed class MensagemSuporteCriadaEmailHandler(
    IContaRepository contaRepository,
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    IEmailService emailService,
    IOptions<EmailSettings> emailSettings,
    ILogger<MensagemSuporteCriadaEmailHandler> logger) : IDomainEventHandler<MensagemSuporteCriadaEvent>
{
    public async Task HandleAsync(MensagemSuporteCriadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (!emailService.Habilitado) return;

        var conta = await contaRepository.ObterPorIdAsync(domainEvent.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
        {
            // Durável: propaga pro worker retentar — conta sumir entre commit e dispatch é anômalo,
            // logar e propagar evita engolir falha real (ticket persistiu mas e-mail não saiu)
            logger.LogWarning("MensagemSuporteCriadaEmailHandler: conta {ContaId} não encontrada.", domainEvent.ContaId);
            throw new InvalidOperationException($"Conta {domainEvent.ContaId} não encontrada para mensagem de suporte.");
        }

        var nome = conta.TipoConta switch
        {
            TipoConta.Aluno => (await alunoRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Nome,
            TipoConta.Treinador => (await treinadorRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Nome,
            _ => null
        } ?? "(nome indisponível)";

        await emailService.EnviarAsync(
            emailSettings.Value.SupportAddress,
            $"[Suporte] {domainEvent.Assunto}",
            EmailTemplates.MensagemSuporte(
                nome,
                conta.Email.Value,
                TipoContaPtBr(conta.TipoConta),
                CategoriaPtBr(domainEvent.Categoria),
                domainEvent.Assunto,
                domainEvent.Descricao),
            cancellationToken,
            replyTo: conta.Email.Value).ConfigureAwait(false);
    }

    private static string TipoContaPtBr(TipoConta tipo) => tipo switch
    {
        TipoConta.Aluno => "Aluno",
        TipoConta.Treinador => "Treinador",
        TipoConta.SystemAdmin => "Administrador",
        _ => tipo.ToString()
    };

    private static string CategoriaPtBr(CategoriaSuporte categoria) => categoria switch
    {
        CategoriaSuporte.Duvida => "Dúvida",
        CategoriaSuporte.Sugestao => "Sugestão",
        CategoriaSuporte.Outro => "Outro",
        _ => categoria.ToString()
    };
}
