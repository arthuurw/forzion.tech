using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Pacotes.ExcluirPacote;

public class ExcluirPacoteHandler(
    IPacoteRepository pacoteRepository,
    IUnitOfWork unitOfWork,
    ILogAprovacaoRepository logRepository,
    ILogger<ExcluirPacoteHandler> logger,
    TimeProvider timeProvider)
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
            return Result.Failure(Error.Conflict("pacote.possui_alunos", "Não é possível excluir um pacote com alunos vinculados."));

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.ExclusaoPacote,
            command.TreinadorId,
            command.PacoteId,
            nameof(Pacote),
            agora);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);
        await logRepository.AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);

        pacoteRepository.Remover(pacote);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Pacote {PacoteId} excluído por treinador {TreinadorId}.", command.PacoteId, command.TreinadorId);

        return Result.Success();
    }
}
