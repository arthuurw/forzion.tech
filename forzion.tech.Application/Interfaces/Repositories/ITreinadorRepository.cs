using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITreinadorRepository
{
    Task<Treinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Treinador?> ObterPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Treinador>> ListarAtivosAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Treinador> Items, int Total)> ListarAsync(TreinadorStatus? status, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Treinador treinador, CancellationToken cancellationToken = default);
    Task ExcluirComDependenciasAsync(Treinador treinador, Guid adminId, CancellationToken cancellationToken = default);
    Task<int> ContarPorStatusAsync(TreinadorStatus status, CancellationToken cancellationToken = default);
}
