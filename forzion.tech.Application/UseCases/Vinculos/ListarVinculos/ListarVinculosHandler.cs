using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Vinculos.ListarVinculos;

public record VinculoDetalheResponse(
    Guid VinculoId,
    Guid TreinadorId,
    Guid AlunoId,
    Guid? PacoteId,
    VinculoStatus Status,
    string NomeAluno,
    string? EmailAluno,
    DateTime CreatedAt,
    bool TemVinculoAtivoPrevio,
    bool PreservarNoLimite)
{
    public static VinculoDetalheResponse De(VinculoComDetalheAluno x) => new(
        x.Vinculo.Id,
        x.Vinculo.TreinadorId,
        x.Vinculo.AlunoId,
        x.Vinculo.PacoteId,
        x.Vinculo.Status,
        x.NomeAluno,
        x.EmailAluno,
        x.Vinculo.CreatedAt,
        x.TemVinculoAtivoPrevio,
        x.Vinculo.PreservarNoLimite);
}

public record ListarVinculosResponse(
    IReadOnlyList<VinculoDetalheResponse> Items,
    int Total,
    int Pagina,
    int TamanhoPagina);

public class ListarVinculosHandler(IVinculoTreinadorAlunoRepository vinculoRepository)
{
    public virtual async Task<ListarVinculosResponse> HandleAsync(
        Guid treinadorId, VinculoStatus? status, int pagina, int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await vinculoRepository
            .ListarComDetalhesAsync(treinadorId, status, pagina, tamanhoPagina, cancellationToken)
            .ConfigureAwait(false);

        var response = items.Select(VinculoDetalheResponse.De).ToList();

        return new ListarVinculosResponse(response, total, pagina, tamanhoPagina);
    }
}
