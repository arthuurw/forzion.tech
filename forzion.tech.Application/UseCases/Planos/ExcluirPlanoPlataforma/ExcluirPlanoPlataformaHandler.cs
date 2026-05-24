using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Planos.ExcluirPlanoPlataforma;

public class ExcluirPlanoPlataformaHandler(
    IPlanoPlataformaRepository planoRepository,
    IUnitOfWork unitOfWork)
{
    public virtual Task HandleAsync(
        ExcluirPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task HandleAsyncCore(
        ExcluirPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        var plano = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false)
            ?? throw new PlanoPlataformaNaoEncontradoException();

        plano.Inativar();

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
