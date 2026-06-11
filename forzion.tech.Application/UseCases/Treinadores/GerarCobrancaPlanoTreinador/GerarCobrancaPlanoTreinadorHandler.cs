using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.GerarCobrancaPlanoTreinador;

public class GerarCobrancaPlanoTreinadorHandler(
    IAssinaturaTreinadorRepository assinaturaRepository,
    IPagamentoTreinadorRepository pagamentoRepository,
    IPlanoPlataformaRepository planoRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    CriarPagamentoComIntentService criarPagamentoService,
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
            return Result.Failure<IniciarPagamentoPlanoResponse>(AssinaturaTreinadorErrors.NaoEncontrada);

        if (assinatura.Status == AssinaturaTreinadorStatus.Cancelada)
            return Result.Failure<IniciarPagamentoPlanoResponse>(AssinaturaTreinadorErrors.NaoPodeRenovarCancelada);

        if (assinatura.Status == AssinaturaTreinadorStatus.Pendente)
            return Result.Failure<IniciarPagamentoPlanoResponse>(AssinaturaTreinadorErrors.NaoPodeRenovarPendente);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (assinatura.PlanoPlataformaIdAgendado.HasValue)
        {
            var planoAgendado = await planoRepository.ObterPorIdAsync(assinatura.PlanoPlataformaIdAgendado.Value, cancellationToken).ConfigureAwait(false);
            if (planoAgendado is not null)
            {
                if (planoAgendado.Preco <= 0)
                {
                    assinatura.Cancelar(now);
                    await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
                    logger.LogInformation("Assinatura {Id} encerrada por downgrade para plano Free.", assinatura.Id);
                    return Result.Failure<IniciarPagamentoPlanoResponse>(Error.Business(
                        "plano_free_assinatura_cancelada", "Downgrade para plano Free: assinatura encerrada sem cobrança."));
                }

                var aplicarResult = assinatura.AplicarPlanoAgendado(planoAgendado.Preco, now);
                if (aplicarResult.IsFailure)
                    logger.LogWarning("Falha ao aplicar plano agendado {PlanoId} na assinatura {AssinaturaId}: {Erro}. Prosseguindo com plano atual.",
                        assinatura.PlanoPlataformaIdAgendado, assinatura.Id, aplicarResult.Error!.Message);
            }
            else
            {
                logger.LogWarning("Plano agendado {PlanoId} não encontrado (excluído?) — agendamento removido da assinatura {AssinaturaId}.",
                    assinatura.PlanoPlataformaIdAgendado, assinatura.Id);
                assinatura.LimparPlanoAgendado(now);
            }
        }

        var params_ = new CriarPagamentoComIntentParams<PagamentoTreinador>(
            ObterPendente: ct => pagamentoRepository.ObterPendentePorAssinaturaAsync(assinatura.Id, ct),
            VerificarIdempotencia: pendente => pendente.StripePaymentIntentId is not null ? pendente : null,
            CriarPagamento: () => PagamentoTreinador.Criar(
                assinatura.TreinadorId, assinatura.Id, assinatura.Valor,
                FinalidadePagamentoTreinador.Renovacao, now, command.Metodo),
            AplicarIntentPix: async (pag, ct) =>
            {
                var r = await stripeService.CriarPixPlataformaPaymentIntentAsync(assinatura.Valor, pag.Id, ct).ConfigureAwait(false);
                return pag.DefinirDadosPix(r.PaymentIntentId, r.QrCode, r.QrCodeUrl, r.Expiracao, now);
            },
            AplicarIntentCartao: async (pag, ct) =>
            {
                var r = await stripeService.CriarCartaoPlataformaPaymentIntentAsync(assinatura.Valor, pag.Id, ct).ConfigureAwait(false);
                return pag.DefinirDadosCartao(r.PaymentIntentId, r.ClientSecret, now);
            },
            AdicionarAsync: (pag, ct) => pagamentoRepository.AdicionarAsync(pag, ct),
            Metodo: command.Metodo
        )
        { MarcarFalhou = (pag, agora) => pag.MarcarFalhou(agora) };

        var result = await criarPagamentoService.ExecutarAsync(params_, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return Result.Failure<IniciarPagamentoPlanoResponse>(result.Error!);

        logger.LogInformation("Cobrança {Metodo} de renovação gerada para assinatura {AssinaturaTreinadorId}, pagamento {PagamentoId}.",
            command.Metodo, assinatura.Id, result.Value.Id);

        return Result.Success(IniciarPagamentoPlanoResponse.De(result.Value));
    }
}
