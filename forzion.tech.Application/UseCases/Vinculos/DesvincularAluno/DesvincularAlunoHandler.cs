using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Vinculos.DesvincularAluno;

public class DesvincularAlunoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    ILogger<DesvincularAlunoHandler> logger)
{
    public virtual async Task HandleAsync(
        DesvincularAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vinculo = await vinculoRepository.ObterPorIdAsync(command.VinculoId, cancellationToken).ConfigureAwait(false)
            ?? throw new VinculoNaoEncontradoException();

        vinculo.Inativar();

        var treinoAlunos = await treinoAlunoRepository.ListarAtivosPorParAsync(vinculo.TreinadorId, vinculo.AlunoId, cancellationToken).ConfigureAwait(false);
        foreach (var ta in treinoAlunos)
            ta.AlterarStatus(TreinoAlunoStatus.Inativo);

        var log = LogAprovacao.Registrar(
            TipoAcaoAprovacao.InativacaoVinculo,
            command.RealizadoPorId,
            vinculo.Id,
            nameof(VinculoTreinadorAluno),
            command.Observacao);

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Vínculo {VinculoId} inativado por {RealizadoPorId}. {Count} ficha(s) afetada(s).", vinculo.Id, command.RealizadoPorId, treinoAlunos.Count);
    }
}
