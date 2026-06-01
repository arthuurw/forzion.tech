using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Planos.ExcluirPlanoPlataforma;

public class ExcluirPlanoPlataformaHandler(
    IPlanoPlataformaRepository planoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result> HandleAsync(
        ExcluirPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        ExcluirPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        var plano = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false);
        if (plano is null)
            return Result.Failure(Error.NotFound("plano_nao_encontrado", "Plano não encontrado."));

        plano.Inativar(timeProvider.GetUtcNow().UtcDateTime);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
