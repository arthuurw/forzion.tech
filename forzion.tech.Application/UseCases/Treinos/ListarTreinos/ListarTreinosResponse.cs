using forzion.tech.Application.UseCases.Treinos;

namespace forzion.tech.Application.UseCases.Treinos.ListarTreinos;

public record ListarTreinosResponse(
    IReadOnlyList<TreinoResponse> Items,
    int Total,
    int Pagina,
    int TamanhoPagina);
