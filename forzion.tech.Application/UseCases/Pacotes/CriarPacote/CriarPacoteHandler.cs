using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Pacotes.CriarPacote;

public class CriarPacoteHandler(
    IPacoteRepository pacoteRepository,
    IUnitOfWork unitOfWork,
    IValidator<CriarPacoteCommand> validator,
    TimeProvider timeProvider,
    ILogger<CriarPacoteHandler> logger)
{
    public virtual Task<Result<PacoteResponse>> HandleAsync(
        CriarPacoteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<PacoteResponse>> HandleAsyncCore(
        CriarPacoteCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var pacoteResult = Pacote.Criar(command.TreinadorId, command.Nome, command.Preco, timeProvider.GetUtcNow().UtcDateTime, command.Descricao);
        if (pacoteResult.IsFailure)
            return Result.Failure<PacoteResponse>(pacoteResult.Error!);
        var pacote = pacoteResult.Value;

        await pacoteRepository.AdicionarAsync(pacote, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Pacote {PacoteId} criado pelo treinador {TreinadorId}.", pacote.Id, command.TreinadorId);

        return Result.Success(PacoteResponseExtensions.ToResponse(pacote));
    }
}
