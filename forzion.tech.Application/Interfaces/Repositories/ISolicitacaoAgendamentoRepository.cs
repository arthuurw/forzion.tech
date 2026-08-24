using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ISolicitacaoAgendamentoRepository
{
    Task AdicionarAsync(SolicitacaoAgendamento solicitacao, CancellationToken cancellationToken = default);

    Task<SolicitacaoAgendamento?> ObterPorIdAsync(Guid id, Guid treinadorId, CancellationToken cancellationToken = default);

    Task<SolicitacaoAgendamento?> ObterPorIdempotencyKeyAsync(Guid treinadorId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<int> ContarConfirmadasSobrepostasAsync(Guid treinadorId, Guid pacoteId, DateTime inicioUtc, DateTime fimUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitacaoAgendamento>> ListarConfirmadasNoIntervaloAsync(Guid treinadorId, Guid pacoteId, DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SolicitacaoAgendamento> Items, int Total)> ListarPorTreinadorAsync(
        Guid treinadorId,
        SolicitacaoAgendamentoStatus? status,
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default);
}
