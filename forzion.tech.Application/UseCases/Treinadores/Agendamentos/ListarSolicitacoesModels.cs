using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.Agendamentos;

public record ListarSolicitacoesQuery(
    Guid TreinadorId,
    SolicitacaoAgendamentoStatus? Status = null,
    int Pagina = 1,
    int TamanhoPagina = 20);

public record ListarSolicitacoesResponse(
    IReadOnlyList<SolicitacaoAgendamentoListItem> Items,
    int Total,
    int Pagina,
    int TamanhoPagina);
