using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Handlers;

public sealed class AlunoAtualizadoSincronizarAssinanteHandler(
    IAssinanteRepository assinanteRepository,
    IUnitOfWork unitOfWork,
    ILogger<AlunoAtualizadoSincronizarAssinanteHandler> logger) : IDomainEventHandler<AlunoAtualizadoEvent>
{
    public async Task HandleAsync(AlunoAtualizadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var assinante = await assinanteRepository.ObterPorAlunoIdAsync(domainEvent.AlunoId, cancellationToken).ConfigureAwait(false);
        if (assinante is null)
        {
            logger.LogWarning("Assinante {AlunoId} não encontrado para sincronização — evento ignorado.", domainEvent.AlunoId);
            return;
        }

        assinante.Sincronizar(domainEvent.Nome, domainEvent.Email, domainEvent.OcorridoEm);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Assinante {AlunoId} sincronizado na projeção billing.", domainEvent.AlunoId);
    }
}
