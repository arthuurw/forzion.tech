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
    IAssinaturaAlunoRepository assinaturaRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    TimeProvider timeProvider,
    ILogger<DesvincularAlunoHandler> logger)
{
    public virtual Task HandleAsync(
        DesvincularAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task HandleAsyncCore(
        DesvincularAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        var vinculo = await vinculoRepository.ObterPorIdAsync(command.VinculoId, cancellationToken).ConfigureAwait(false)
            ?? throw new VinculoNaoEncontradoException();

        // Validar autorização
        if (!userContext.IsSystemAdmin && vinculo.TreinadorId != userContext.PerfilId)
            throw new AcessoNegadoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var inativarResult = vinculo.Inativar(agora);
        if (inativarResult.IsFailure)
            throw new DomainException(inativarResult.Error!.Message);

        var assinatura = await assinaturaRepository.ObterPorVinculoIdAsync(vinculo.Id, cancellationToken).ConfigureAwait(false);
        if (assinatura is not null && assinatura.Status != Domain.Enums.AssinaturaAlunoStatus.Cancelada)
        {
            var cancelarResult = assinatura.Cancelar(agora);
            if (cancelarResult.IsFailure)
                throw new DomainException(cancelarResult.Error!.Message);
        }

        var treinoAlunos = await treinoAlunoRepository.ListarAtivosPorParAsync(vinculo.TreinadorId, vinculo.AlunoId, cancellationToken).ConfigureAwait(false);
        foreach (var ta in treinoAlunos)
            ta.AlterarStatus(TreinoAlunoStatus.Inativo, agora);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.InativacaoVinculo,
            userContext.PerfilId,
            vinculo.Id,
            nameof(VinculoTreinadorAluno),
            agora,
            command.Observacao);
        if (logResult.IsFailure)
            throw new DomainException(logResult.Error!.Message);
        var log = logResult.Value;

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Vínculo {VinculoId} inativado por {RealizadoPorId}. {Count} ficha(s) afetada(s).", vinculo.Id, userContext.PerfilId, treinoAlunos.Count);
    }
}
