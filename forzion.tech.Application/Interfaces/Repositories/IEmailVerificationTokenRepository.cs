using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IEmailVerificationTokenRepository
{
    Task AdicionarAsync(EmailVerificationToken token, CancellationToken cancellationToken = default);
    Task<EmailVerificationToken?> BuscarPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
}
