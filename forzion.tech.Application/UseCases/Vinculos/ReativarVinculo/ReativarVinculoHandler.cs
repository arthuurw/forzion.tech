using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Vinculos.ReativarVinculo;

public class ReativarVinculoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IAlunoRepository alunoRepository,
    ILimiteTreinadorService limiteTreinadorService,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ReativarVinculoHandler> logger)
{
    public virtual Task<VinculoResponse> HandleAsync(
        ReativarVinculoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<VinculoResponse> HandleAsyncCore(
        ReativarVinculoCommand command,
        CancellationToken cancellationToken = default)
    {
        var aluno = await alunoRepository.ObterPorIdAsync(command.AlunoId, cancellationToken).ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        if (aluno.Status != AlunoStatus.Ativo)
            throw new DomainException("Aluno inativo não pode ter vínculo reativado.");

        var vinculoAtivo = await vinculoRepository.ObterAtivoPorAlunoAsync(command.AlunoId, cancellationToken).ConfigureAwait(false);
        if (vinculoAtivo is not null)
            throw new AlunoJaVinculadoException();

        await limiteTreinadorService.ValidarAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var vinculoResult = VinculoTreinadorAluno.Criar(command.TreinadorId, command.AlunoId, agora);
        if (vinculoResult.IsFailure)
            throw new DomainException(vinculoResult.Error!.Message);
        var vinculo = vinculoResult.Value;

        var aprovarResult = vinculo.Aprovar(command.TreinadorId, command.PacoteId, agora);
        if (aprovarResult.IsFailure)
            throw new DomainException(aprovarResult.Error!.Message);

        await vinculoRepository.AdicionarAsync(vinculo, cancellationToken).ConfigureAwait(false);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AprovacaoVinculo,
            command.TreinadorId,
            vinculo.Id,
            nameof(VinculoTreinadorAluno),
            agora);
        if (logResult.IsFailure)
            throw new DomainException(logResult.Error!.Message);
        var log = logResult.Value;

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Vínculo reativado entre treinador {TreinadorId} e aluno {AlunoId}.", command.TreinadorId, command.AlunoId);

        return new VinculoResponse(vinculo.Id, vinculo.TreinadorId, vinculo.AlunoId, vinculo.PacoteId, vinculo.Status, vinculo.CreatedAt);
    }
}
