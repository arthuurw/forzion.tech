using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface INotificacaoRepository
{
    Task AdicionarAsync(Notificacao notificacao, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notificacao>> ListarPorContaAsync(Guid contaId, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> ContarNaoLidasAsync(Guid contaId, CancellationToken cancellationToken = default);

    Task<bool> MarcarLidaAsync(Guid id, Guid contaId, DateTime agora, CancellationToken cancellationToken = default);

    Task<int> PurgarAntesDeAsync(DateTime limite, CancellationToken cancellationToken = default);
}
