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
    ILogger<ReativarVinculoHandler> logger)
{
    public virtual async Task<VinculoResponse> HandleAsync(
        ReativarVinculoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _ = await alunoRepository.ObterPorIdAsync(command.AlunoId, cancellationToken).ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        var vinculoAtivo = await vinculoRepository.ObterAtivoPorAlunoAsync(command.AlunoId, cancellationToken).ConfigureAwait(false);
        if (vinculoAtivo is not null)
            throw new AlunoJaVinculadoException();

        await limiteTreinadorService.ValidarAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);

        var vinculo = VinculoTreinadorAluno.Criar(command.TreinadorId, command.AlunoId);
        vinculo.Aprovar(command.TreinadorId, command.PacoteAlunoId);

        await vinculoRepository.AdicionarAsync(vinculo, cancellationToken).ConfigureAwait(false);

        var log = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AprovacaoVinculo,
            command.TreinadorId,
            vinculo.Id,
            nameof(VinculoTreinadorAluno));

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Vínculo reativado entre treinador {TreinadorId} e aluno {AlunoId}.", command.TreinadorId, command.AlunoId);

        return new VinculoResponse(vinculo.Id, vinculo.TreinadorId, vinculo.AlunoId, vinculo.PacoteAlunoId, vinculo.Status, vinculo.CreatedAt);
    }
}
