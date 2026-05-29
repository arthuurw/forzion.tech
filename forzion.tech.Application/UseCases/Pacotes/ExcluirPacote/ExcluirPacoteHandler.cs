using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Pacotes.ExcluirPacote;

public class ExcluirPacoteHandler(
    IPacoteRepository pacoteRepository,
    IUnitOfWork unitOfWork)
{
    public virtual Task<Result> HandleAsync(
        ExcluirPacoteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        ExcluirPacoteCommand command,
        CancellationToken cancellationToken = default)
    {
        var pacote = await pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false)
            ?? throw new PacoteNaoEncontradoException();

        if (pacote.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        var temVinculos = await pacoteRepository.ExisteVinculoComPacoteAsync(command.PacoteId, cancellationToken).ConfigureAwait(false);
        if (temVinculos)
            return Result.Failure(Error.Business("Não é possível excluir um pacote com alunos vinculados."));

        pacoteRepository.Remover(pacote);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
