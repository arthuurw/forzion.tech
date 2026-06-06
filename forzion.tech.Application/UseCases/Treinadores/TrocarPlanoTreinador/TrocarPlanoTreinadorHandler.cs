using System.Data;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.TrocarPlanoTreinador;

public class TrocarPlanoTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IAssinaturaTreinadorRepository assinaturaRepository,
    IPlanoPlataformaRepository planoRepository,
    IPagamentoTreinadorRepository pagamentoRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    IDbContextTransactionProvider transactionProvider,
    TimeProvider timeProvider,
    ILogger<TrocarPlanoTreinadorHandler> logger)
{
    private const int DiasCiclo = 30;

    public virtual Task<Result<TrocarPlanoTreinadorResponse>> HandleAsync(
        TrocarPlanoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<TrocarPlanoTreinadorResponse>> HandleAsyncCore(
        TrocarPlanoTreinadorCommand command,
        CancellationToken cancellationToken)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var novoPlano = await planoRepository.ObterPorIdAsync(command.NovoPlanoPlataformaId, cancellationToken).ConfigureAwait(false);
        if (novoPlano is null)
            return Result.Failure<TrocarPlanoTreinadorResponse>(Error.NotFound("plano_plataforma_nao_encontrado", "Plano não encontrado."));

        if (!novoPlano.IsAtivo)
            return Result.Failure<TrocarPlanoTreinadorResponse>(Error.Business("plano_plataforma_inativo", "O plano selecionado não está ativo."));

        if (novoPlano.Tier == TierPlano.Elite)
            return Result.Failure<TrocarPlanoTreinadorResponse>(PlanoPlataformaErrors.EliteIndisponivel);

        var assinatura = await assinaturaRepository.ObterAtualPorTreinadorAsync(treinador.Id, cancellationToken).ConfigureAwait(false);
        if (assinatura is null)
            return Result.Failure<TrocarPlanoTreinadorResponse>(Error.NotFound("assinatura_treinador_nao_encontrada", "Nenhuma assinatura ativa encontrada."));

        if (assinatura.Status == AssinaturaTreinadorStatus.Cancelada)
            return Result.Failure<TrocarPlanoTreinadorResponse>(AssinaturaTreinadorErrors.JaCancelada);

        if (assinatura.PlanoPlataformaId == command.NovoPlanoPlataformaId)
            return Result.Failure<TrocarPlanoTreinadorResponse>(Error.Business("mesmo_plano", "O treinador já está neste plano."));

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (assinatura.Status == AssinaturaTreinadorStatus.Inadimplente)
            return await ProcessarInadimplenteAsync(assinatura, novoPlano, command.Metodo, agora, cancellationToken).ConfigureAwait(false);

        if (novoPlano.Preco > assinatura.Valor)
            return await ProcessarUpgradeAsync(assinatura, novoPlano, command.Metodo, agora, cancellationToken).ConfigureAwait(false);

        return await ProcessarDowngradeAsync(assinatura, novoPlano, agora, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<TrocarPlanoTreinadorResponse>> ProcessarUpgradeAsync(
        AssinaturaTreinador assinatura, PlanoPlataforma novoPlano, MetodoPagamento metodo, DateTime agora, CancellationToken ct)
    {
        var diasRestantes = Math.Max(0, (int)(assinatura.DataProximaCobranca - agora).TotalDays);
        var valorProracao = Math.Round((novoPlano.Preco - assinatura.Valor) * diasRestantes / DiasCiclo, 2, MidpointRounding.AwayFromZero);

        if (valorProracao <= 0)
        {
            var trocarResult = assinatura.TrocarPlanoImediato(novoPlano.Id, novoPlano.Preco, agora);
            if (trocarResult.IsFailure)
                return Result.Failure<TrocarPlanoTreinadorResponse>(trocarResult.Error!);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            logger.LogInformation("Upgrade imediato (proração zero) para treinador {TreinadorId}, plano {PlanoId}.", assinatura.TreinadorId, novoPlano.Id);
            return Result.Success(TrocarPlanoTreinadorResponse.UpgradeImediato(agora));
        }

        PagamentoTreinador pagamento;
        await using (var tx = await transactionProvider.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false))
        {
            var pendente = await pagamentoRepository.ObterPendentePorAssinaturaAsync(assinatura.Id, ct).ConfigureAwait(false);
            if (pendente is not null)
            {
                // Idempotência: mesma troca em curso — reutiliza o intent existente.
                if (pendente.Finalidade == FinalidadePagamentoTreinador.TrocaPlano
                    && pendente.PlanoAlvoId == novoPlano.Id
                    && pendente.StripePaymentIntentId is not null)
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return Result.Success(TrocarPlanoTreinadorResponse.Upgrade(pendente));
                }

                // Zumbi (sem intent) ou troca para plano diferente: marca Falhou.
                // Se havia intent Stripe ativo para plano distinto, o webhook payment_intent.succeeded
                // encontrará Status != Pendente e retornará JaConsistente — sem risco de aplicar plano errado.
                if (pendente.StripePaymentIntentId is not null)
                    logger.LogWarning("PagamentoTreinador ativo {PagamentoId} para plano {PlanoAlvoId} descartado em favor de nova troca para {NovoPlanoId}.",
                        pendente.Id, pendente.PlanoAlvoId, novoPlano.Id);
                else
                    logger.LogWarning("PagamentoTreinador zumbi {PagamentoId} na troca de plano. Marcando como Falhou.", pendente.Id);

                var marcarResult = pendente.MarcarFalhou(agora);
                if (marcarResult.IsFailure)
                    return Result.Failure<TrocarPlanoTreinadorResponse>(marcarResult.Error!);
            }

            var pagamentoResult = PagamentoTreinador.Criar(
                assinatura.TreinadorId, assinatura.Id, valorProracao,
                FinalidadePagamentoTreinador.TrocaPlano, agora, metodo, novoPlano.Id);
            if (pagamentoResult.IsFailure)
                return Result.Failure<TrocarPlanoTreinadorResponse>(pagamentoResult.Error!);
            pagamento = pagamentoResult.Value;

            if (metodo == MetodoPagamento.Cartao)
            {
                var cartaoResult = await stripeService.CriarCartaoPlataformaPaymentIntentAsync(valorProracao, pagamento.Id, ct).ConfigureAwait(false);
                var definir = pagamento.DefinirDadosCartao(cartaoResult.PaymentIntentId, cartaoResult.ClientSecret, agora);
                if (definir.IsFailure) return Result.Failure<TrocarPlanoTreinadorResponse>(definir.Error!);
            }
            else
            {
                var pixResult = await stripeService.CriarPixPlataformaPaymentIntentAsync(valorProracao, pagamento.Id, ct).ConfigureAwait(false);
                var definir = pagamento.DefinirDadosPix(pixResult.PaymentIntentId, pixResult.QrCode, pixResult.QrCodeUrl, pixResult.Expiracao, agora);
                if (definir.IsFailure) return Result.Failure<TrocarPlanoTreinadorResponse>(definir.Error!);
            }

            await pagamentoRepository.AdicionarAsync(pagamento, ct).ConfigureAwait(false);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }

        logger.LogInformation("Upgrade proração {Valor} gerado para treinador {TreinadorId}, plano alvo {PlanoId}.",
            valorProracao, assinatura.TreinadorId, novoPlano.Id);
        return Result.Success(TrocarPlanoTreinadorResponse.Upgrade(pagamento));
    }

    private async Task<Result<TrocarPlanoTreinadorResponse>> ProcessarDowngradeAsync(
        AssinaturaTreinador assinatura, PlanoPlataforma novoPlano, DateTime agora, CancellationToken ct)
    {
        var agendarResult = assinatura.AgendarDowngrade(novoPlano.Id, agora);
        if (agendarResult.IsFailure)
            return Result.Failure<TrocarPlanoTreinadorResponse>(agendarResult.Error!);

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Downgrade agendado para treinador {TreinadorId}, plano {PlanoId} na próxima renovação {Data}.",
            assinatura.TreinadorId, novoPlano.Id, assinatura.DataProximaCobranca);
        return Result.Success(TrocarPlanoTreinadorResponse.Downgrade(assinatura.DataProximaCobranca));
    }

    private async Task<Result<TrocarPlanoTreinadorResponse>> ProcessarInadimplenteAsync(
        AssinaturaTreinador assinatura, PlanoPlataforma novoPlano, MetodoPagamento metodo, DateTime agora, CancellationToken ct)
    {
        PagamentoTreinador pagamento;
        await using (var tx = await transactionProvider.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false))
        {
            var pendente = await pagamentoRepository.ObterPendentePorAssinaturaAsync(assinatura.Id, ct).ConfigureAwait(false);
            if (pendente is not null)
            {
                if (pendente.Finalidade == FinalidadePagamentoTreinador.TrocaPlano
                    && pendente.PlanoAlvoId == novoPlano.Id
                    && pendente.StripePaymentIntentId is not null)
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return Result.Success(TrocarPlanoTreinadorResponse.Regularizacao(pendente));
                }

                if (pendente.StripePaymentIntentId is not null)
                    logger.LogWarning("PagamentoTreinador ativo {PagamentoId} para plano {PlanoAlvoId} descartado (inadimplente trocando para {NovoPlanoId}).",
                        pendente.Id, pendente.PlanoAlvoId, novoPlano.Id);

                var marcarResult = pendente.MarcarFalhou(agora);
                if (marcarResult.IsFailure)
                    return Result.Failure<TrocarPlanoTreinadorResponse>(marcarResult.Error!);
            }

            var pagamentoResult = PagamentoTreinador.Criar(
                assinatura.TreinadorId, assinatura.Id, novoPlano.Preco,
                FinalidadePagamentoTreinador.TrocaPlano, agora, metodo, novoPlano.Id);
            if (pagamentoResult.IsFailure)
                return Result.Failure<TrocarPlanoTreinadorResponse>(pagamentoResult.Error!);
            pagamento = pagamentoResult.Value;

            if (metodo == MetodoPagamento.Cartao)
            {
                var cartaoResult = await stripeService.CriarCartaoPlataformaPaymentIntentAsync(novoPlano.Preco, pagamento.Id, ct).ConfigureAwait(false);
                var definir = pagamento.DefinirDadosCartao(cartaoResult.PaymentIntentId, cartaoResult.ClientSecret, agora);
                if (definir.IsFailure) return Result.Failure<TrocarPlanoTreinadorResponse>(definir.Error!);
            }
            else
            {
                var pixResult = await stripeService.CriarPixPlataformaPaymentIntentAsync(novoPlano.Preco, pagamento.Id, ct).ConfigureAwait(false);
                var definir = pagamento.DefinirDadosPix(pixResult.PaymentIntentId, pixResult.QrCode, pixResult.QrCodeUrl, pixResult.Expiracao, agora);
                if (definir.IsFailure) return Result.Failure<TrocarPlanoTreinadorResponse>(definir.Error!);
            }

            await pagamentoRepository.AdicionarAsync(pagamento, ct).ConfigureAwait(false);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }

        logger.LogInformation("Regularização de inadimplente via troca de plano {PlanoId} para treinador {TreinadorId}.",
            novoPlano.Id, assinatura.TreinadorId);
        return Result.Success(TrocarPlanoTreinadorResponse.Regularizacao(pagamento));
    }
}
