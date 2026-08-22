using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.TestDoubles;

// NullEmailService (Test env, sem Resend:ApiKey) nunca expõe o token cru — ele só existe em
// memória dentro do handler. Substitui ILeadConviteSender para o E2E de conversão poder seguir
// a cadeia lead→convite→cadastro sem depender do canal de e-mail real.
public sealed class LeadConviteTokenCaptureSender : ILeadConviteSender
{
    public string? UltimoTokenCapturado { get; private set; }

    public Task<bool> EnviarAsync(
        TipoContatoLead contatoTipo,
        string contatoValor,
        string nomeLead,
        string nomeTreinador,
        string tokenCru,
        CancellationToken cancellationToken = default)
    {
        UltimoTokenCapturado = tokenCru;
        return Task.FromResult(true);
    }
}
