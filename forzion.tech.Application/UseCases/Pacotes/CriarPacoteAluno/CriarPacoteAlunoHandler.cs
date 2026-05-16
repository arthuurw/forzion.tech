using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Pacotes.CriarPacoteAluno;

public class CriarPacoteAlunoHandler(
    IPacoteAlunoRepository pacoteRepository,
    IUnitOfWork unitOfWork,
    IValidator<CriarPacoteAlunoCommand> validator,
    ILogger<CriarPacoteAlunoHandler> logger)
{
    public virtual Task<PacoteAlunoResponse> HandleAsync(
        CriarPacoteAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<PacoteAlunoResponse> HandleAsyncCore(
        CriarPacoteAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var pacote = PacoteAluno.Criar(command.TreinadorId, command.Nome, command.Preco, command.Descricao);

        await pacoteRepository.AdicionarAsync(pacote, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("PacoteAluno {PacoteId} criado pelo treinador {TreinadorId}.", pacote.Id, command.TreinadorId);

        return PacoteAlunoResponseExtensions.ToResponse(pacote);
    }
}
