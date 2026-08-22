using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class LeadConviteRepository(AppDbContext context) : ILeadConviteRepository
{
    public async Task AdicionarAsync(LeadConvite convite, CancellationToken cancellationToken = default) =>
        await context.LeadConvites.AddAsync(convite, cancellationToken).ConfigureAwait(false);

    public async Task<LeadConvite?> ObterPorTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await context.LeadConvites
            .FirstOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

    public async Task<LeadConvite?> ObterAtivoPorLeadAsync(Guid treinadorId, Guid leadId, CancellationToken cancellationToken = default) =>
        await context.LeadConvites
            .FirstOrDefaultAsync(
                c => c.TreinadorId == treinadorId && c.LeadId == leadId && c.UsadoEm == null && c.InvalidadoEm == null,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> LimparExpiradosOuConsumidosAsync(DateTime agora, CancellationToken cancellationToken = default) =>
        await context.LeadConvites
            .Where(c => c.ExpiraEm <= agora || c.UsadoEm != null)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
