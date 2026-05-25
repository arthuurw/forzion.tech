using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Handlers;

public sealed class AlunoRegistradoSincronizarAssinanteHandler(
    IAssinanteRepository assinanteRepository,
    IUnitOfWork unitOfWork,
    ILogger<AlunoRegistradoSincronizarAssinanteHandler> logger) : IDomainEventHandler<AlunoRegistradoEvent>
{
    public async Task HandleAsync(AlunoRegistradoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var assinante = Assinante.Criar(domainEvent.AlunoId, domainEvent.Nome, domainEvent.Email, domainEvent.OcorridoEm);
        await assinanteRepository.AdicionarAsync(assinante, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Assinante {AlunoId} criado na projeção billing.", domainEvent.AlunoId);
    }
}
