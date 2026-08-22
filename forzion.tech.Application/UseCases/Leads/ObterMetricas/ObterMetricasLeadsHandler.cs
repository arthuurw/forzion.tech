using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Leads.ObterMetricas;

public class ObterMetricasLeadsHandler(ILeadRepository leadRepository)
{
    public virtual Task<ObterMetricasLeadsResponse> HandleAsync(
        ObterMetricasLeadsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<ObterMetricasLeadsResponse> HandleAsyncCore(
        ObterMetricasLeadsQuery query,
        CancellationToken cancellationToken)
    {
        var metricas = await leadRepository
            .AgregarMetricasAsync(query.TreinadorId, query.Inicio, query.Fim, cancellationToken)
            .ConfigureAwait(false);

        var taxaConversao = metricas.Total == 0 ? 0d : (double)metricas.Convertidos / metricas.Total;

        return new ObterMetricasLeadsResponse(
            metricas.Total,
            metricas.PorOrigemAgente,
            metricas.PorOrigemManual,
            metricas.Convertidos,
            taxaConversao,
            metricas.PorMotivoDescarte);
    }
}
