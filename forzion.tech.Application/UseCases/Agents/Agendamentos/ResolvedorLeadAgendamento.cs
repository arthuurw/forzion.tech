using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Agents.Agendamentos;

// D-I: uma pessoa = uma ficha. O parâmetro é ContatoLead (não string crua) de propósito — só
// existe via ContatoLead.Criar, então o lookup por contato.Valor é sempre o valor já normalizado,
// nunca a string do payload (specification-design-review §5).
public class ResolvedorLeadAgendamento(ILeadRepository leadRepository)
{
    public virtual async Task<Result<Lead>> ResolverAsync(
        Guid treinadorId,
        string nome,
        ContatoLead contato,
        ConsentimentoLead consentimento,
        OrigemLead? origem,
        DateTime slotInicioUtc,
        DateTime agora,
        CancellationToken cancellationToken = default)
    {
        var existente = await leadRepository
            .ObterReutilizavelPorContatoAsync(treinadorId, contato.Valor, cancellationToken)
            .ConfigureAwait(false);

        if (existente is null)
            return Lead.Criar(treinadorId, nome, contato, null, consentimento, origem, LeadSource.Agent, null, null, agora);

        var interacaoResult = existente.RegistrarInteracao(Guid.Empty, ObservacaoInteracao(slotInicioUtc), agora);
        return interacaoResult.IsFailure
            ? Result.Failure<Lead>(interacaoResult.Error!)
            : Result.Success(existente);
    }

    private static string ObservacaoInteracao(DateTime slotInicioUtc) =>
        $"Solicitação de agendamento via agente para {slotInicioUtc:yyyy-MM-dd HH:mm} UTC";
}
