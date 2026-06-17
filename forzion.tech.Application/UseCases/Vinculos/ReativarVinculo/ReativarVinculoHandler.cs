using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Vinculos.ReativarVinculo;

public class ReativarVinculoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IAlunoRepository alunoRepository,
    IPacoteRepository pacoteRepository,
    ILimiteTreinadorService limiteTreinadorService,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ReativarVinculoHandler> logger)
{
    public virtual Task<Result<VinculoResponse>> HandleAsync(
        ReativarVinculoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<VinculoResponse>> HandleAsyncCore(
        ReativarVinculoCommand command,
        CancellationToken cancellationToken = default)
    {
        var aluno = await alunoRepository.ObterPorIdAsync(command.AlunoId, cancellationToken).ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        if (aluno.Status != AlunoStatus.Ativo)
            return Result.Failure<VinculoResponse>(Error.Business("vinculo.aluno_inativo", "Aluno inativo não pode ter vínculo reativado."));

        var vinculoAtivo = await vinculoRepository.ObterAtivoPorAlunoAsync(command.AlunoId, cancellationToken).ConfigureAwait(false);
        if (vinculoAtivo is not null)
            throw new AlunoJaVinculadoException();

        await limiteTreinadorService.ValidarAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);

        if (command.PacoteId != Guid.Empty)
        {
            var pacote = await pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false);
            if (pacote is not null && pacote.TreinadorId != command.TreinadorId)
                return Result.Failure<VinculoResponse>(PacoteErrors.NaoPertenceTreinador);
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var vinculoResult = VinculoTreinadorAluno.Criar(command.TreinadorId, command.AlunoId, agora);
        if (vinculoResult.IsFailure)
            return Result.Failure<VinculoResponse>(vinculoResult.Error!);
        var vinculo = vinculoResult.Value;

        var aprovarResult = vinculo.Aprovar(command.TreinadorId, command.PacoteId, agora);
        if (aprovarResult.IsFailure)
            return Result.Failure<VinculoResponse>(aprovarResult.Error!);

        await vinculoRepository.AdicionarAsync(vinculo, cancellationToken).ConfigureAwait(false);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AprovacaoVinculo,
            command.TreinadorId,
            vinculo.Id,
            nameof(VinculoTreinadorAluno),
            agora);
        if (logResult.IsFailure)
            return Result.Failure<VinculoResponse>(logResult.Error!);
        var log = logResult.Value;

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Vínculo reativado entre treinador {TreinadorId} e aluno {AlunoId}.", command.TreinadorId, command.AlunoId);

        return Result.Success(new VinculoResponse(vinculo.Id, vinculo.TreinadorId, vinculo.AlunoId, vinculo.PacoteId, vinculo.Status, vinculo.CreatedAt));
    }
}
