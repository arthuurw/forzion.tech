using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Agents.Agendamentos;

// D-I: uma pessoa = uma ficha. O parâmetro é ContatoLead (não string crua) de propósito — só
// existe via ContatoLead.Criar, então o lookup por contato.Valor é sempre o valor já normalizado,
// nunca a string do payload (specification-design-review §5).
public class ResolvedorLeadAgendamento(
    ILeadRepository leadRepository,
    IUnitOfWork unitOfWork,
    IDatabaseErrorInspector databaseErrorInspector)
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
        {
            var novoLeadResult = Lead.Criar(treinadorId, nome, contato, null, consentimento, origem, LeadSource.Agent, null, null, agora);
            if (novoLeadResult.IsFailure)
                return novoLeadResult;

            await leadRepository.AdicionarAsync(novoLeadResult.Value, cancellationToken).ConfigureAwait(false);

            // Commit dedicado (não delegado ao handler chamador): a criação do lead precisa
            // colidir com a UNIQUE parcial de contato ativo ANTES de a solicitação ser
            // montada em cima dele — senão o handler prossegue com um lead que nunca vai persistir.
            try
            {
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            // Corrida: duas solicitações concorrentes com idempotencyKey distintas resolvem o
            // mesmo contato novo e colidem na UNIQUE de leads(treinador_id, contato_valor) — a
            // violação significa que o concorrente já gravou primeiro; relê e reusa a ficha
            // vencedora (precedente RegistrarExecucaoHandler, specification-concurrency §4).
            catch (Exception ex) when (databaseErrorInspector.EhViolacaoDeUnicidade(ex))
            {
                unitOfWork.DescartarAlteracoesPendentes();

                var vencedor = await leadRepository
                    .ObterReutilizavelPorContatoAsync(treinadorId, contato.Valor, cancellationToken)
                    .ConfigureAwait(false);
                if (vencedor is null)
                    throw;

                return RegistrarInteracaoNoLead(vencedor, slotInicioUtc, agora);
            }

            return novoLeadResult;
        }

        return RegistrarInteracaoNoLead(existente, slotInicioUtc, agora);
    }

    private static Result<Lead> RegistrarInteracaoNoLead(Lead lead, DateTime slotInicioUtc, DateTime agora)
    {
        var interacaoResult = lead.RegistrarInteracao(Guid.Empty, ObservacaoInteracao(slotInicioUtc), agora);
        return interacaoResult.IsFailure
            ? Result.Failure<Lead>(interacaoResult.Error!)
            : Result.Success(lead);
    }

    private static string ObservacaoInteracao(DateTime slotInicioUtc) =>
        $"Solicitação de agendamento via agente para {slotInicioUtc:yyyy-MM-dd HH:mm} UTC";
}
