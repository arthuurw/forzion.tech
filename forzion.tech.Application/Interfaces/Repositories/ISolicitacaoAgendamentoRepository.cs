using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ISolicitacaoAgendamentoRepository
{
    Task AdicionarAsync(SolicitacaoAgendamento solicitacao, CancellationToken cancellationToken = default);

    Task<SolicitacaoAgendamento?> ObterPorIdAsync(Guid id, Guid treinadorId, CancellationToken cancellationToken = default);

    Task<SolicitacaoAgendamento?> ObterPorIdempotencyKeyAsync(Guid treinadorId, string idempotencyKey, CancellationToken cancellationToken = default);

    // Sem pacoteId: a agenda do treinador é o recurso escasso (AD-021) — slots de todos os
    // pacotes derivam do mesmo HorarioFuncionamento, então a confirmada de QUALQUER pacote
    // sobrepõe o mesmo horário do treinador.
    Task<int> ContarConfirmadasSobrepostasAsync(Guid treinadorId, DateTime inicioUtc, DateTime fimUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitacaoAgendamento>> ListarConfirmadasNoIntervaloAsync(Guid treinadorId, DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SolicitacaoAgendamentoListItem> Items, int Total)> ListarPorTreinadorAsync(
        Guid treinadorId,
        SolicitacaoAgendamentoStatus? status,
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default);
}
