using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Assinaturas.CancelarAssinatura;

public class CancelarAssinaturaHandler(
    IAssinaturaRepository assinaturaRepository,
    IUnitOfWork unitOfWork,
    ILogger<CancelarAssinaturaHandler> logger)
{
    public virtual async Task<Result> HandleAsync(
        CancelarAssinaturaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var assinatura = await assinaturaRepository.ObterPorIdAsync(command.AssinaturaId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Assinatura não encontrada.");

        try
        {
            assinatura.Cancelar();
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Business(ex.Message));
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Assinatura {AssinaturaId} cancelada.", assinatura.Id);

        return Result.Success();
    }
}
