using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.ContratarPlanoTreinador;

public class ContratarPlanoTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IAssinaturaTreinadorRepository assinaturaRepository,
    IPlanoPlataformaRepository planoRepository,
    IPagamentoTreinadorRepository pagamentoRepository,
    IStripeService stripeService,
    CriarPagamentoComIntentService criarPagamentoService,
    IUnitOfWork unitOfWork,
    IDatabaseErrorInspector databaseErrorInspector,
    TimeProvider timeProvider,
    ILogger<ContratarPlanoTreinadorHandler> logger)
{
    public virtual Task<Result<ContratarPlanoTreinadorResponse>> HandleAsync(
        ContratarPlanoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<ContratarPlanoTreinadorResponse>> HandleAsyncCore(
        ContratarPlanoTreinadorCommand command,
        CancellationToken cancellationToken)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (treinador.Status != TreinadorStatus.Ativo)
            return Result.Failure<ContratarPlanoTreinadorResponse>(
                Error.Conflict("treinador.nao_elegivel_contratacao", "O treinador não está elegível para contratar um plano."));

        var plano = await planoRepository.ObterPorIdAsync(command.PlanoPlataformaId, cancellationToken).ConfigureAwait(false);
        if (plano is null)
            return Result.Failure<ContratarPlanoTreinadorResponse>(Error.NotFound("plano_plataforma_nao_encontrado", "Plano não encontrado."));
        if (!plano.IsAtivo)
            return Result.Failure<ContratarPlanoTreinadorResponse>(Error.Business("plano_plataforma.inativo", "O plano selecionado não está ativo."));
        if (plano.Tier == TierPlano.Elite)
            return Result.Failure<ContratarPlanoTreinadorResponse>(PlanoPlataformaErrors.EliteIndisponivel);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var assinaturaResult = await ObterOuCriarAssinaturaAsync(treinador.Id, plano, agora, cancellationToken).ConfigureAwait(false);
        if (assinaturaResult.IsFailure)
            return Result.Failure<ContratarPlanoTreinadorResponse>(assinaturaResult.Error!);
        var assinatura = assinaturaResult.Value;

        var chave = IdempotencyKey.Cobranca("contratacao", assinatura.Id, agora);

        PixPaymentResult? pix = null;
        CartaoPaymentResult? cartao = null;

        var params_ = new CriarPagamentoComIntentParams<PagamentoTreinador>(
            CriarIntent: async ct =>
            {
                if (command.Metodo == MetodoPagamento.Cartao)
                    cartao = await stripeService.CriarCartaoPlataformaPaymentIntentAsync(assinatura.Valor, chave, ct).ConfigureAwait(false);
                else
                    pix = await stripeService.CriarPixPlataformaPaymentIntentAsync(assinatura.Valor, chave, ct).ConfigureAwait(false);
            },
            ObterPendente: ct => pagamentoRepository.ObterPendentePorAssinaturaAsync(assinatura.Id, ct),
            VerificarIdempotencia: pendente =>
                pendente.Finalidade == FinalidadePagamentoTreinador.Contratacao
                && pendente.StripePaymentIntentId is not null
                    ? pendente : null,
            CriarPagamento: () => PagamentoTreinador.Criar(
                treinador.Id, assinatura.Id, assinatura.Valor,
                FinalidadePagamentoTreinador.Contratacao, agora, command.Metodo),
            AplicarIntent: pag => command.Metodo == MetodoPagamento.Cartao
                ? pag.DefinirDadosCartao(cartao!.PaymentIntentId, cartao.ClientSecret, agora)
                : pag.DefinirDadosPix(pix!.PaymentIntentId, pix.QrCode, pix.QrCodeUrl, pix.Expiracao, agora),
            AdicionarAsync: (pag, ct) => pagamentoRepository.AdicionarAsync(pag, ct)
        )
        { MarcarFalhou = (pag, dataAgora) => pag.MarcarFalhou(dataAgora) };

        var result = await criarPagamentoService.ExecutarAsync(params_, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return Result.Failure<ContratarPlanoTreinadorResponse>(result.Error!);

        logger.LogInformation("Contratação {Metodo} iniciada para treinador {TreinadorId}, assinatura {AssinaturaId}, pagamento {PagamentoId}.",
            command.Metodo, treinador.Id, assinatura.Id, result.Value.Id);

        return Result.Success(ContratarPlanoTreinadorResponse.De(result.Value));
    }

    private async Task<Result<AssinaturaTreinador>> ObterOuCriarAssinaturaAsync(
        Guid treinadorId, PlanoPlataforma plano, DateTime agora, CancellationToken ct)
    {
        var atual = await assinaturaRepository.ObterAtualPorTreinadorAsync(treinadorId, ct).ConfigureAwait(false);
        if (atual is not null)
            return EhPendenteReutilizavel(atual, plano.Id)
                ? Result.Success(atual)
                : Result.Failure<AssinaturaTreinador>(
                    Error.Conflict("treinador.assinatura_ja_existe", "O treinador já possui uma assinatura."));

        var criarResult = AssinaturaTreinador.Criar(treinadorId, plano.Id, plano.Preco, agora);
        if (criarResult.IsFailure)
            return Result.Failure<AssinaturaTreinador>(criarResult.Error!);

        try
        {
            await assinaturaRepository.AdicionarAsync(criarResult.Value, ct).ConfigureAwait(false);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            return Result.Success(criarResult.Value);
        }
        catch (Exception ex) when (databaseErrorInspector.EhViolacaoDeUnicidade(ex))
        {
            var vencedor = await assinaturaRepository.ObterAtualPorTreinadorAsync(treinadorId, ct).ConfigureAwait(false);
            if (vencedor is null || !EhPendenteReutilizavel(vencedor, plano.Id))
                return Result.Failure<AssinaturaTreinador>(
                    Error.Conflict("treinador.assinatura_ja_existe", "O treinador já possui uma assinatura."));
            return Result.Success(vencedor);
        }
    }

    private static bool EhPendenteReutilizavel(AssinaturaTreinador assinatura, Guid planoId) =>
        assinatura.Status == AssinaturaTreinadorStatus.Pendente && assinatura.PlanoPlataformaId == planoId;
}
