using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ILeadConviteRepository
{
    Task AdicionarAsync(LeadConvite convite, CancellationToken cancellationToken = default);

    Task<LeadConvite?> ObterPorTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<LeadConvite?> ObterAtivoPorLeadAsync(Guid treinadorId, Guid leadId, CancellationToken cancellationToken = default);

    Task<int> LimparExpiradosOuConsumidosAsync(DateTime agora, CancellationToken cancellationToken = default);
}
