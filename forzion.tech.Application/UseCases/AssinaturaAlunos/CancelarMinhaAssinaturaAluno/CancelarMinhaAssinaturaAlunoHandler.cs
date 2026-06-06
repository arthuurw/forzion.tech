using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
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
///   <item><description>Nenhuma assinatura ativa/inadimplente → <c>not_found</c> (mapeia 404 no endpoint).</description></item>
///   <item><description>Já cancelada (race) → <c>business_error</c> (mapeia 422).</description></item>
/// </list>
/// </summary>
public class CancelarMinhaAssinaturaAlunoHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IPagamentoRepository pagamentoRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CancelarMinhaAssinaturaAlunoHandler> logger)
{
    public const string AssinaturaNaoEncontradaErrorCode = "assinatura_nao_encontrada";

    private const int PrazoArrependimentoDias = 7;

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

        try
        {
            assinatura.Cancelar(agora);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Business(ex.Message));
        }

        if ((agora - assinatura.DataInicio).TotalDays <= PrazoArrependimentoDias)
            await ReembolsarPrimeiraContratacaoAsync(assinatura.Id, cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Aluno {AlunoId} cancelou a própria assinatura {AssinaturaAlunoId}.",
            command.AlunoId, assinatura.Id);

        return Result.Success();
    }

    private async Task ReembolsarPrimeiraContratacaoAsync(Guid assinaturaId, CancellationToken cancellationToken)
    {
        var pagamentos = await pagamentoRepository
            .ListarPorAssinaturaAlunoAsync(assinaturaId, cancellationToken)
            .ConfigureAwait(false);

        var pago = pagamentos
            .Where(p => p.Status == PagamentoStatus.Pago && !string.IsNullOrEmpty(p.StripePaymentIntentId))
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefault();

        if (pago is null) return;

        try
        {
            // Charge destino (aluno paga na plataforma com TransferData) → reverter transferência e fee.
            // O status Estornado chega depois via webhook charge.refunded — não muta síncrono aqui.
            await stripeService.CriarReembolsoAsync(pago.StripePaymentIntentId!, reverterTransferencia: true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // CDC: reembolso é direito do consumidor, mas falha no Stripe NÃO bloqueia o cancelamento.
            logger.LogCritical(ex,
                "Falha ao reembolsar pagamento {PaymentIntentId} da assinatura {AssinaturaAlunoId} no cancelamento de 7 dias. Cancelamento prossegue; reembolso manual necessário.",
                pago.StripePaymentIntentId, assinaturaId);
        }
    }
}
