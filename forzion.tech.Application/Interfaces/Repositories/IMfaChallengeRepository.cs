using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IMfaChallengeRepository
{
    Task AdicionarAsync(MfaChallenge challenge, CancellationToken cancellationToken = default);
    Task<MfaChallenge?> BuscarUltimoPorContaEPropositoAsync(Guid contaId, MfaProposito proposito, CancellationToken cancellationToken = default);
}
