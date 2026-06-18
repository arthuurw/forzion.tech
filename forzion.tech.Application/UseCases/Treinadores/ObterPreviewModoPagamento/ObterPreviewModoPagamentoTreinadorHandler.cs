using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Treinadores.ObterPreviewModoPagamento;

public record PreviewModoPagamentoResponse(int AssinaturasAtivasAlunos, int VinculosCobravelSemAssinatura);

public class ObterPreviewModoPagamentoTreinadorHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository)
{
    public virtual async Task<PreviewModoPagamentoResponse> HandleAsync(
        ObterPreviewModoPagamentoTreinadorQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var assinaturas = await assinaturaRepository
            .ListarNaoCanceladasPorTreinadorAsync(query.TreinadorId, cancellationToken).ConfigureAwait(false);
        var jaCobertos = assinaturas.Select(a => a.VinculoId).ToHashSet();

        var vinculos = await vinculoRepository
            .ListarAtivosPorTreinadorAsync(query.TreinadorId, cancellationToken).ConfigureAwait(false);
        var cobravelSemAssinatura = vinculos.Count(v => v.PacoteId is not null && !jaCobertos.Contains(v.Id));

        return new PreviewModoPagamentoResponse(assinaturas.Count, cobravelSemAssinatura);
    }
}
