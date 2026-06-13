using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IRefreshTokenFamilyRepository
{
    Task AdicionarAsync(RefreshTokenFamily familia, CancellationToken cancellationToken = default);
    Task<RefreshTokenFamily?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefreshTokenFamily>> ListarAtivasPorContaAsync(Guid contaId, DateTime agora, CancellationToken cancellationToken = default);

    /// <summary>Purga famílias revogadas ou que ultrapassaram o teto absoluto (GC).</summary>
    Task<int> LimparExpiradasAsync(DateTime agora, CancellationToken cancellationToken = default);

    /// <summary>Apaga TODAS as famílias (e tokens, via cascade) de uma conta — purga LGPD na anonimização.</summary>
    Task<int> ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
}
