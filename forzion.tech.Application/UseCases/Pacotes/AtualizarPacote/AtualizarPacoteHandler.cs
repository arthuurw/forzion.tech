using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Pacotes.AtualizarPacote;

public class AtualizarPacoteHandler(
    IPacoteRepository pacoteRepository,
    IUnitOfWork unitOfWork,
    IValidator<AtualizarPacoteCommand> validator,
    TimeProvider timeProvider)
{
    public virtual Task<Result<PacoteResponse>> HandleAsync(
        AtualizarPacoteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<PacoteResponse>> HandleAsyncCore(
        AtualizarPacoteCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var pacote = await pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false)
            ?? throw new PacoteNaoEncontradoException();

        if (pacote.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        var atualizarResult = pacote.Atualizar(command.Nome, command.Preco, command.Descricao, timeProvider.GetUtcNow().UtcDateTime);
        if (atualizarResult.IsFailure)
            return Result.Failure<PacoteResponse>(atualizarResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(PacoteResponseExtensions.ToResponse(pacote));
    }
}
