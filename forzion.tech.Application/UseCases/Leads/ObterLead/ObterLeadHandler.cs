using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Leads.ObterLead;

public class ObterLeadHandler(ILeadRepository leadRepository)
{
    public virtual Task<Result<LeadDetalheResponse>> HandleAsync(
        ObterLeadQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<Result<LeadDetalheResponse>> HandleAsyncCore(
        ObterLeadQuery query,
        CancellationToken cancellationToken)
    {
        var lead = await leadRepository
            .ObterComHistoricoAsync(query.TreinadorId, query.LeadId, cancellationToken)
            .ConfigureAwait(false);
        if (lead is null)
            return Result.Failure<LeadDetalheResponse>(LeadErrors.NaoEncontrado);

        var historico = lead.Interacoes
            .OrderBy(i => i.OcorridaEm)
            .Select(i => new LeadInteracaoResponse(i.Id, i.OcorridaEm, i.StatusAnterior, i.StatusNovo, i.Observacao, i.RealizadoPorId))
            .ToList();

        return Result.Success(new LeadDetalheResponse(
            lead.Id,
            lead.Nome,
            lead.Contato.Tipo,
            lead.Contato.Valor,
            lead.Interesse,
            lead.Consentimento.Finalidade,
            lead.Consentimento.ConcedidoEm,
            lead.Consentimento.RegistradoEm,
            lead.Origem?.UserAgent,
            lead.Origem?.Assistente,
            lead.Source,
            lead.Status,
            lead.MotivoDescarte,
            lead.AlunoId,
            lead.Anonimizado,
            lead.UltimoToqueEm,
            lead.CreatedAt,
            lead.UpdatedAt,
            historico));
    }
}
