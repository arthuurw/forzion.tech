using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Pacotes.AtualizarPacote;

public class AtualizarPacoteHandler(
    IPacoteRepository pacoteRepository,
    IUnitOfWork unitOfWork,
    IValidator<AtualizarPacoteCommand> validator)
{
    public virtual Task<PacoteResponse> HandleAsync(
        AtualizarPacoteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<PacoteResponse> HandleAsyncCore(
        AtualizarPacoteCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var pacote = await pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false)
            ?? throw new PacoteNaoEncontradoException();

        if (pacote.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        pacote.Atualizar(command.Nome, command.Preco, command.Descricao);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PacoteResponseExtensions.ToResponse(pacote);
    }
}
