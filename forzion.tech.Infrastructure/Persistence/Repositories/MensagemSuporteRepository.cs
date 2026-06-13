using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class MensagemSuporteRepository(AppDbContext context) : IMensagemSuporteRepository
{
    public async Task AdicionarAsync(MensagemSuporte mensagem, CancellationToken cancellationToken = default) =>
        await context.MensagensSuporte.AddAsync(mensagem, cancellationToken).ConfigureAwait(false);

    public async Task ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await context.MensagensSuporte
            .Where(m => m.ContaId == contaId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
