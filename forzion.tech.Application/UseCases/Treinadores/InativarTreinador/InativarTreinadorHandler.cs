using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.InativarTreinador;

public class InativarTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IPacoteRepository pacoteRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<InativarTreinadorHandler> logger)
{
    public virtual Task<Result> HandleAsync(
        InativarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        InativarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var inativarResult = treinador.Inativar(agora, command.AdminId);
        if (inativarResult.IsFailure)
            return Result.Failure(inativarResult.Error!);

        var vinculos = await vinculoRepository.ListarAtivosPorTreinadorAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);

        foreach (var vinculo in vinculos)
        {
            var vinculoResult = vinculo.Inativar(agora);
            if (vinculoResult.IsFailure)
                return Result.Failure(vinculoResult.Error!);
            var treinoAlunos = await treinoAlunoRepository.ListarAtivosPorParAsync(command.TreinadorId, vinculo.AlunoId, cancellationToken).ConfigureAwait(false);
            foreach (var ta in treinoAlunos)
                ta.AlterarStatus(TreinoAlunoStatus.Inativo, agora);
        }

        var pacotes = await pacoteRepository.ListarAtivosPorTreinadorAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);
        foreach (var pacote in pacotes)
            pacote.Inativar(agora);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.InativacaoTreinador,
            command.AdminId,
            treinador.Id,
            nameof(Treinador),
            agora,
            command.Observacao);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);
        var log = logResult.Value;

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treinador {TreinadorId} inativado por {AdminId}. {Count} vínculo(s) afetado(s).", treinador.Id, command.AdminId, vinculos.Count);

        return Result.Success();
    }
}
