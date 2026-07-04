using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface INotificacaoRepository
{
    /// <summary>
    /// Idempotente para nudges do scan: colisão no índice único parcial
    /// (destinatario_conta_id, tipo, dia_referencia) vira no-op (23505 engolido),
    /// permitindo reexecução do scan no mesmo dia sem duplicar.
    /// </summary>
    Task AdicionarAsync(Notificacao notificacao, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notificacao>> ListarPorContaAsync(Guid contaId, int skip, int take, CancellationToken cancellationToken = default);

    Task<int> ContarNaoLidasAsync(Guid contaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scoped por <paramref name="contaId"/> (anti-IDOR): marcar notificação de outro
    /// dono afeta 0 linhas e retorna false, permitindo ao endpoint responder 404.
    /// </summary>
    Task<bool> MarcarLidaAsync(Guid id, Guid contaId, DateTime agora, CancellationToken cancellationToken = default);

    Task<int> PurgarAntesDeAsync(DateTime limite, CancellationToken cancellationToken = default);
}
