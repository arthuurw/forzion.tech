using System.Data;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.AlterarModoPagamento;

public class AlterarModoPagamentoTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    IAssinaturaAlunoRepository assinaturaRepository,
    IPagamentoRepository pagamentoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    CriarAssinaturaAlunoService criarAssinaturaService,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    IDbContextTransactionProvider transactionProvider,
    IValidator<AlterarModoPagamentoTreinadorCommand> validator,
    TimeProvider timeProvider,
    ILogger<AlterarModoPagamentoTreinadorHandler> logger,
    ILogAprovacaoRepository logRepository)
{
    public virtual Task<Result<AlterarModoPagamentoResponse>> HandleAsync(
        AlterarModoPagamentoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<AlterarModoPagamentoResponse>> HandleAsyncCore(
        AlterarModoPagamentoTreinadorCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        if (!Enum.IsDefined(command.NovoModo))
            return Result.Failure<AlterarModoPagamentoResponse>(TreinadorErrors.ModoPagamentoInvalido);

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        // Curto-circuito antes do gate de onboarding: trocar para o modo atual é no-op, não exige Stripe.
        if (command.NovoModo == treinador.ModoPagamentoAluno)
            return Result.Failure<AlterarModoPagamentoResponse>(TreinadorErrors.ModoPagamentoInalterado);

        // Voltar à plataforma sem Stripe configurado deixaria o treinador sem como cobrar — barra antes de mutar.
        if (command.NovoModo == ModoPagamentoAluno.Plataforma)
        {
            var conta = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);
            if (conta is null || !conta.OnboardingCompleto)
                return Result.Failure<AlterarModoPagamentoResponse>(TreinadorErrors.ConfigureStripePrimeiro);
        }

        var alterarResult = treinador.AlterarModoPagamento(command.NovoModo, agora);
        if (alterarResult.IsFailure)
            return Result.Failure<AlterarModoPagamentoResponse>(alterarResult.Error!);

        var txResult = await transactionProvider.ExecuteInTransactionAsync(IsolationLevel.Serializable, async (tx, _) =>
        {
            var paymentIntents = new List<string>();
            var criadas = 0;
            var ignorados = 0;

            if (command.NovoModo == ModoPagamentoAluno.Externo)
                paymentIntents = await MigrarParaExternoAsync(command.TreinadorId, agora, cancellationToken).ConfigureAwait(false);
            else
                (criadas, ignorados) = await MigrarParaPlataformaAsync(command.TreinadorId, agora, cancellationToken).ConfigureAwait(false);

            var logResult = await logRepository.RegistrarAsync(
                TipoAcaoAprovacao.AlteracaoModoPagamentoTreinador,
                command.TreinadorId,
                command.TreinadorId,
                nameof(Treinador),
                agora,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (logResult.IsFailure)
                return Result.Failure<(List<string> PaymentIntents, int Criadas, int Ignorados)>(logResult.Error!);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success((paymentIntents, criadas, ignorados));
        }, cancellationToken).ConfigureAwait(false);

        if (txResult.IsFailure)
            return Result.Failure<AlterarModoPagamentoResponse>(txResult.Error!);

        var (paymentIntentsParaCancelar, assinaturasCriadas, vinculosIgnorados) = txResult.Value;

        // Efeito externo só APÓS o commit (espelha CancelarMinhaAssinaturaAlunoHandler): se o commit
        // falhasse, não cancelaríamos Pix no Stripe sem o estado persistido. Best-effort por PI.
        foreach (var paymentIntentId in paymentIntentsParaCancelar)
        {
            try
            {
                var outcome = await stripeService.CancelarPaymentIntentAsync(paymentIntentId, cancellationToken).ConfigureAwait(false);
                switch (outcome)
                {
                    case CancelarPaymentIntentResultado.JaCapturado:
                        logger.LogCritical(
                            "PaymentIntent {PaymentIntentId} já capturado na troca para Externo (treinador {TreinadorId}). Reconciliação por refund necessária.",
                            paymentIntentId, command.TreinadorId);
                        break;
                    case CancelarPaymentIntentResultado.JaCancelado:
                        logger.LogDebug("PaymentIntent {PaymentIntentId} já estava cancelado na troca para Externo.", paymentIntentId);
                        break;
                    default:
                        logger.LogInformation("PaymentIntent {PaymentIntentId} cancelado na troca para Externo.", paymentIntentId);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha transitória ao cancelar PaymentIntent {PaymentIntentId} na troca para modo Externo.", paymentIntentId);
            }
        }

        logger.LogInformation("Treinador {TreinadorId} alterou modo de pagamento para {Modo}.", command.TreinadorId, command.NovoModo);

        return Result.Success(new AlterarModoPagamentoResponse(
            treinador.ModoPagamentoAluno, treinador.ModoPagamentoAlunoAlteradoEm!.Value, assinaturasCriadas, vinculosIgnorados));
    }

    private async Task<List<string>> MigrarParaExternoAsync(Guid treinadorId, DateTime agora, CancellationToken cancellationToken)
    {
        var paymentIntents = new List<string>();
        var assinaturas = await assinaturaRepository.ListarNaoCanceladasPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        foreach (var assinatura in assinaturas)
        {
            assinatura.Cancelar(agora);
            // Bulk administrativo do treinador: suprime o e-mail/WhatsApp por-aluno do evento de cancelamento.
            assinatura.ClearDomainEvents();

            var pendente = await pagamentoRepository.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, cancellationToken).ConfigureAwait(false);
            if (pendente?.StripePaymentIntentId is { } paymentIntentId)
            {
                // Deixa Pendente: webhook resolve esse PI (canceled→Expirado, succeeded→refund SEC1).
                // Expirar aqui cegaria o refund de um Pix capturado.
                paymentIntents.Add(paymentIntentId);
            }
            else
            {
                pendente?.MarcarExpirado(agora);
            }
        }
        return paymentIntents;
    }

    private async Task<(int Criadas, int Ignorados)> MigrarParaPlataformaAsync(Guid treinadorId, DateTime agora, CancellationToken cancellationToken)
    {
        var jaCobertos = (await assinaturaRepository.ListarNaoCanceladasPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false))
            .Select(a => a.VinculoId)
            .ToHashSet();

        var criadas = 0;
        var ignorados = 0;
        var vinculos = await vinculoRepository.ListarAtivosPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        foreach (var vinculo in vinculos)
        {
            if (vinculo.PacoteId is null || jaCobertos.Contains(vinculo.Id))
                continue;

            var resultado = await criarAssinaturaService.CriarParaVinculoAsync(vinculo, agora, suprimirNotificacao: true, cancellationToken).ConfigureAwait(false);
            if (resultado == ResultadoCriacaoAssinaturaAluno.Criada)
                criadas++;
            else
                ignorados++;
        }
        return (criadas, ignorados);
    }
}
