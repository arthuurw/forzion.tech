using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Planos.ExcluirPlanoTreinador;

public class ExcluirPlanoTreinadorHandler(
    IPlanoTreinadorRepository planoRepository,
    IUnitOfWork unitOfWork)
{
    public virtual async Task HandleAsync(
        ExcluirPlanoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plano = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Plano não encontrado.");

        plano.Inativar();

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
