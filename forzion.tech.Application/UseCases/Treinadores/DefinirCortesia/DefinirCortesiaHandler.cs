using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.DefinirCortesia;

public class DefinirCortesiaHandler(
    ITreinadorRepository treinadorRepository,
    IPlanoPlataformaRepository planoRepository,
    IAssinaturaTreinadorRepository assinaturaRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<DefinirCortesiaHandler> logger)
{
    public virtual Task<Result<TreinadorResponse>> HandleAsync(
        DefinirCortesiaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<TreinadorResponse>> HandleAsyncCore(
        DefinirCortesiaCommand command,
        CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (command.PlanoId is { } planoId)
        {
            var plano = await planoRepository.ObterPorIdAsync(planoId, cancellationToken).ConfigureAwait(false)
                ?? throw new PlanoPlataformaNaoEncontradoException();

            if (plano.Tier == TierPlano.Elite)
                return Result.Failure<TreinadorResponse>(PlanoPlataformaErrors.EliteIndisponivel);

            var assinatura = await assinaturaRepository.ObterAtualPorTreinadorAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);
            var valorPago = assinatura is not null && assinatura.Status == AssinaturaTreinadorStatus.Ativa
                ? assinatura.Valor
                : 0m;

            if (plano.Preco < valorPago)
                return Result.Failure<TreinadorResponse>(TreinadorErrors.CortesiaAbaixoDoPago);
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var definirResult = treinador.DefinirCortesia(command.PlanoId, agora);
        if (definirResult.IsFailure)
            return Result.Failure<TreinadorResponse>(definirResult.Error!);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.DefinicaoCortesiaTreinador,
            command.AdminId,
            treinador.Id,
            nameof(Treinador),
            agora,
            command.PlanoId is { } id ? $"Cortesia definida para o plano {id}." : "Cortesia removida.");
        if (logResult.IsFailure)
            return Result.Failure<TreinadorResponse>(logResult.Error!);
        var log = logResult.Value;

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Cortesia {PlanoId} definida para o treinador {TreinadorId}.", command.PlanoId, treinador.Id);

        return Result.Success(TreinadorResponse.De(treinador));
    }
}
