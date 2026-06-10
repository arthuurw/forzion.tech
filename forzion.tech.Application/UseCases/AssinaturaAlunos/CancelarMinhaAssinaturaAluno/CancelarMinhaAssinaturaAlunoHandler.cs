using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarMinhaAssinaturaAluno;

/// <summary>
/// Auto-cancelamento de assinatura pelo aluno autenticado. Busca a assinatura
/// "atual" do aluno (Ativa ou Inadimplente — qualquer status não-cancelado
/// retornado por <see cref="IAssinaturaAlunoRepository.ObterAtualPorAlunoAsync"/>),
/// invoca <c>Cancelar(agora)</c> no agregado (dispara
/// <see cref="Domain.Events.AssinaturaAlunoCanceladaEvent"/>) e commita.
///
/// Falhas:
/// <list type="bullet">
///   <item><description>Nenhuma assinatura ativa/inadimplente (ou já cancelada no momento do lookup) → <c>assinatura_nao_encontrada</c> (mapeia 404 no endpoint).</description></item>
///   <item><description>Já cancelada (race pós-guard, via <c>Cancelar</c> no domínio) → <c>assinatura_aluno.ja_cancelada</c> (mapeia 422).</description></item>
/// </list>
/// </summary>
public class CancelarMinhaAssinaturaAlunoHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IPagamentoRepository pagamentoRepository,
    ReembolsoArrependimentoService reembolsoService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CancelarMinhaAssinaturaAlunoHandler> logger)
{
    public const string AssinaturaNaoEncontradaErrorCode = "assinatura_nao_encontrada";

    public virtual async Task<Result> HandleAsync(
        CancelarMinhaAssinaturaAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var assinatura = await assinaturaRepository
            .ObterAtualPorAlunoAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (assinatura is null || assinatura.Status == AssinaturaAlunoStatus.Cancelada)
            return Result.Failure(new Error(
                AssinaturaNaoEncontradaErrorCode,
                "Nenhuma assinatura ativa encontrada para cancelar."));

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var cancelarResult = assinatura.Cancelar(agora);
        if (cancelarResult.IsFailure)
            return cancelarResult;

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        // Reembolso DEPOIS do commit: se o commit falhar, nada é estornado. Falha no estorno
        // pós-commit não reverte o cancelamento (LogCritical + reembolso manual).
        await ReembolsarPrimeiraContratacaoAsync(assinatura.Id, agora, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Aluno {AlunoId} cancelou a própria assinatura {AssinaturaAlunoId}.",
            command.AlunoId, assinatura.Id);

        return Result.Success();
    }

    private async Task ReembolsarPrimeiraContratacaoAsync(Guid assinaturaId, DateTime agora, CancellationToken cancellationToken)
    {
        var pagamentos = await pagamentoRepository
            .ListarPorAssinaturaAlunoAsync(assinaturaId, cancellationToken)
            .ConfigureAwait(false);

        var pago = pagamentos
            .Where(p => p.Status == PagamentoStatus.Pago && !string.IsNullOrEmpty(p.StripePaymentIntentId))
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefault();

        // Charge destino do aluno → reverter transferência e fee (G1). Status Estornado chega
        // depois via webhook charge.refunded — não muta síncrono aqui.
        await reembolsoService
            .ReembolsarSeDentroDoPrazoAsync(pago?.Id ?? Guid.Empty, agora, pago?.StripePaymentIntentId, pago?.DataPagamento, reverterTransferencia: true, cancellationToken)
            .ConfigureAwait(false);
    }
}
