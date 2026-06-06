using System.Data;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.GerarCobrancaPlanoTreinador;

public class GerarCobrancaPlanoTreinadorHandler(
    IAssinaturaTreinadorRepository assinaturaRepository,
    IPagamentoTreinadorRepository pagamentoRepository,
    IPlanoPlataformaRepository planoRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    IDbContextTransactionProvider transactionProvider,
    TimeProvider timeProvider,
    ILogger<GerarCobrancaPlanoTreinadorHandler> logger)
{
    public virtual Task<Result<IniciarPagamentoPlanoResponse>> HandleAsync(
        GerarCobrancaPlanoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<IniciarPagamentoPlanoResponse>> HandleAsyncCore(
        GerarCobrancaPlanoTreinadorCommand command,
        CancellationToken cancellationToken)
    {
        var assinatura = await assinaturaRepository.ObterPorIdAsync(command.AssinaturaTreinadorId, cancellationToken).ConfigureAwait(false);
        if (assinatura is null)
            return Result.Failure<IniciarPagamentoPlanoResponse>(Error.NotFound("assinatura_treinador_nao_encontrada", "AssinaturaTreinador não encontrada."));

        if (assinatura.Status == AssinaturaTreinadorStatus.Cancelada)
            return Result.Failure<IniciarPagamentoPlanoResponse>(Error.Business("assinatura_treinador_cancelada", "Assinatura cancelada não pode ser renovada."));

        if (assinatura.Status == AssinaturaTreinadorStatus.Pendente)
            return Result.Failure<IniciarPagamentoPlanoResponse>(Error.Business("assinatura_treinador_pendente", "Assinatura pendente não pode ser renovada."));

        PagamentoTreinador pagamento;

        // Atomicidade (G-PAY-1): Stripe ANTES do único commit (single-write).
        // Tx serializable protege contra dois callers concorrentes (cron + endpoint manual).
        await using (var tx = await transactionProvider.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false))
        {
            if (assinatura.PlanoPlataformaIdAgendado.HasValue)
            {
                var planoAgendado = await planoRepository.ObterPorIdAsync(assinatura.PlanoPlataformaIdAgendado.Value, cancellationToken).ConfigureAwait(false);
                if (planoAgendado is not null)
                {
                    if (planoAgendado.Preco <= 0)
                    {
                        assinatura.Cancelar(timeProvider.GetUtcNow().UtcDateTime);
                        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
                        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                        logger.LogInformation("Assinatura {Id} encerrada por downgrade para plano Free.", assinatura.Id);
                        return Result.Failure<IniciarPagamentoPlanoResponse>(Error.Business(
                            "plano_free_assinatura_cancelada", "Downgrade para plano Free: assinatura encerrada sem cobrança."));
                    }

                    var aplicarResult = assinatura.AplicarPlanoAgendado(planoAgendado.Preco, timeProvider.GetUtcNow().UtcDateTime);
                    if (aplicarResult.IsFailure)
                        logger.LogWarning("Falha ao aplicar plano agendado {PlanoId} na assinatura {AssinaturaId}: {Erro}. Prosseguindo com plano atual.",
                            assinatura.PlanoPlataformaIdAgendado, assinatura.Id, aplicarResult.Error!.Message);
                }
                else
                {
                    // Plano agendado foi excluído da plataforma: limpa o agendamento e prossegue com plano atual.
                    logger.LogWarning("Plano agendado {PlanoId} não encontrado (excluído?) — agendamento removido da assinatura {AssinaturaId}.",
                        assinatura.PlanoPlataformaIdAgendado, assinatura.Id);
                    assinatura.LimparPlanoAgendado(timeProvider.GetUtcNow().UtcDateTime);
                }
            }

            var pendente = await pagamentoRepository.ObterPendentePorAssinaturaAsync(assinatura.Id, cancellationToken).ConfigureAwait(false);
            if (pendente is not null)
            {
                if (pendente.StripePaymentIntentId is not null)
                {
                    await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return Result.Success(IniciarPagamentoPlanoResponse.De(pendente));
                }

                logger.LogWarning("PagamentoTreinador zumbi {PagamentoId} detectado para assinatura {AssinaturaTreinadorId}. Marcando como Falhou.", pendente.Id, assinatura.Id);
                var marcarZumbiResult = pendente.MarcarFalhou(timeProvider.GetUtcNow().UtcDateTime);
                if (marcarZumbiResult.IsFailure)
                    return Result.Failure<IniciarPagamentoPlanoResponse>(marcarZumbiResult.Error!);
            }

            var pagamentoResult = PagamentoTreinador.Criar(
                assinatura.TreinadorId, assinatura.Id, assinatura.Valor,
                FinalidadePagamentoTreinador.Renovacao, timeProvider.GetUtcNow().UtcDateTime, command.Metodo);
            if (pagamentoResult.IsFailure)
                return Result.Failure<IniciarPagamentoPlanoResponse>(pagamentoResult.Error!);
            pagamento = pagamentoResult.Value;

            if (command.Metodo == MetodoPagamento.Cartao)
            {
                var cartaoResult = await stripeService.CriarCartaoPlataformaPaymentIntentAsync(
                    assinatura.Valor, pagamento.Id, cancellationToken).ConfigureAwait(false);

                var definirCartaoResult = pagamento.DefinirDadosCartao(cartaoResult.PaymentIntentId, cartaoResult.ClientSecret, timeProvider.GetUtcNow().UtcDateTime);
                if (definirCartaoResult.IsFailure)
                    return Result.Failure<IniciarPagamentoPlanoResponse>(definirCartaoResult.Error!);
            }
            else
            {
                var pixResult = await stripeService.CriarPixPlataformaPaymentIntentAsync(
                    assinatura.Valor, pagamento.Id, cancellationToken).ConfigureAwait(false);

                var definirPixResult = pagamento.DefinirDadosPix(pixResult.PaymentIntentId, pixResult.QrCode, pixResult.QrCodeUrl, pixResult.Expiracao, timeProvider.GetUtcNow().UtcDateTime);
                if (definirPixResult.IsFailure)
                    return Result.Failure<IniciarPagamentoPlanoResponse>(definirPixResult.Error!);
            }

            await pagamentoRepository.AdicionarAsync(pagamento, cancellationToken).ConfigureAwait(false);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Cobrança {Metodo} de renovação gerada para assinatura {AssinaturaTreinadorId}, pagamento {PagamentoId}.",
            command.Metodo, assinatura.Id, pagamento.Id);

        return Result.Success(IniciarPagamentoPlanoResponse.De(pagamento));
    }
}
