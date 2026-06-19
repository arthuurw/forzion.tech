using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IPasswordResetTokenRepository
{
    Task AdicionarAsync(PasswordResetToken token, CancellationToken cancellationToken = default);
    Task<PasswordResetToken?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task InvalidarPendentesPorContaAsync(Guid contaId, DateTime agora, CancellationToken cancellationToken = default);
}
