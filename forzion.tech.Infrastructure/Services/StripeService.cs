using System.Globalization;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Pagamentos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace forzion.tech.Infrastructure.Services;

public class StripeService(
    IOptions<StripeSettings> settings,
    TimeProvider timeProvider,
    ILogger<StripeService> logger) : IStripeService
{
    private readonly StripeSettings _settings = settings.Value;

    // C4: Chave passada explicitamente em cada chamada — sem dependência de estado global
    private RequestOptions RequestOptions => new() { ApiKey = _settings.SecretKey };

    /// <summary>
    /// Belt-and-suspenders sobre F12 (serializable tx app-side): Stripe-Idempotency-Key
    /// garante que retry de network/transport NÃO crie 2º PaymentIntent. Stripe responde
    /// idêntico até 24h depois com a mesma key. Key = `pagamento-{guid_n}` é único e
    /// estável por pagamento (re-criar Pagamento gera novo Guid → nova key).
    /// </summary>
    private RequestOptions PaymentIntentRequestOptions(Guid pagamentoId) => new()
    {
        ApiKey = _settings.SecretKey,
        IdempotencyKey = $"pagamento-{pagamentoId:N}"
    };

    public async Task<string> CriarContaConnectAsync(string email, string nome, CancellationToken cancellationToken = default)
    {
        var service = new AccountService();
        var options = new AccountCreateOptions
        {
            Type = "express",
            Country = "BR",
            Email = email,
            Capabilities = new AccountCapabilitiesOptions
            {
                // BR exige card_payments junto de transfers (Stripe rejeita transfers sozinho em contas BR).
                CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true },
            },
            BusinessProfile = new AccountBusinessProfileOptions
            {
                Name = nome,
            },
        };

        var account = await service.CreateAsync(options, requestOptions: RequestOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Conta Stripe Connect criada: {AccountId}.", account.Id);
        return account.Id;
    }

    public async Task<string> GerarLinkOnboardingAsync(
        string stripeAccountId,
        string urlRetorno,
        string urlCancelamento,
        CancellationToken cancellationToken = default)
    {
        var service = new AccountLinkService();
        var options = new AccountLinkCreateOptions
        {
            Account = stripeAccountId,
            RefreshUrl = urlCancelamento,
            ReturnUrl = urlRetorno,
            Type = "account_onboarding",
        };

        var link = await service.CreateAsync(options, requestOptions: RequestOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return link.Url;
    }

    public async Task<PixPaymentResult> CriarPixPaymentIntentAsync(
        decimal valor,
        string stripeAccountId,
        Guid pagamentoId,
        decimal taxaPlataformaPercent,
        CancellationToken cancellationToken = default)
    {
        var (valorCentavos, taxaCentavos) = MoneyCentavos.ValorETaxaCentavos(valor, taxaPlataformaPercent);

        var service = new PaymentIntentService();
        var options = new PaymentIntentCreateOptions
        {
            Amount = valorCentavos,
            Currency = "brl",
            PaymentMethodTypes = ["pix"],
            PaymentMethodOptions = new PaymentIntentPaymentMethodOptionsOptions
            {
                Pix = new PaymentIntentPaymentMethodOptionsPixOptions
                {
                    ExpiresAfterSeconds = 3600,
                },
            },
            Metadata = new Dictionary<string, string>
            {
                ["pagamento_id"] = pagamentoId.ToString(),
            },
            ApplicationFeeAmount = taxaCentavos,
            TransferData = new PaymentIntentTransferDataOptions
            {
                Destination = stripeAccountId,
            },
        };

        var intent = await service.CreateAsync(options, requestOptions: PaymentIntentRequestOptions(pagamentoId), cancellationToken: cancellationToken).ConfigureAwait(false);

        var pix = intent.NextAction?.PixDisplayQrCode
            ?? throw new InvalidOperationException("Stripe não retornou dados Pix.");

        var expiracao = timeProvider.GetUtcNow().UtcDateTime.AddSeconds(3600);

        logger.LogInformation("PaymentIntent Pix {IntentId} criado para conta {AccountId}.", intent.Id, stripeAccountId);

        return new PixPaymentResult(intent.Id, pix.Data, pix.ImageUrlPng, expiracao);
    }

    public async Task<CartaoPaymentResult> CriarCartaoPaymentIntentAsync(
        decimal valor,
        string stripeAccountId,
        Guid pagamentoId,
        decimal taxaPlataformaPercent,
        CancellationToken cancellationToken = default)
    {
        var (valorCentavos, taxaCentavos) = MoneyCentavos.ValorETaxaCentavos(valor, taxaPlataformaPercent);

        var service = new PaymentIntentService();
        var options = new PaymentIntentCreateOptions
        {
            Amount = valorCentavos,
            Currency = "brl",
            PaymentMethodTypes = ["card"],
            Metadata = new Dictionary<string, string>
            {
                ["pagamento_id"] = pagamentoId.ToString(),
            },
            ApplicationFeeAmount = taxaCentavos,
            TransferData = new PaymentIntentTransferDataOptions
            {
                Destination = stripeAccountId,
            },
        };

        var intent = await service.CreateAsync(options, requestOptions: PaymentIntentRequestOptions(pagamentoId), cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("PaymentIntent Cartão {IntentId} criado para conta {AccountId}.", intent.Id, stripeAccountId);

        return new CartaoPaymentResult(intent.Id, intent.ClientSecret);
    }

    private RequestOptions PagamentoTreinadorRequestOptions(Guid pagamentoTreinadorId) => new()
    {
        ApiKey = _settings.SecretKey,
        IdempotencyKey = $"pagamento-treinador-{pagamentoTreinadorId:N}"
    };

    public async Task<PixPaymentResult> CriarPixPlataformaPaymentIntentAsync(
        decimal valor,
        Guid pagamentoTreinadorId,
        CancellationToken cancellationToken = default)
    {
        var (valorCentavos, _) = MoneyCentavos.ValorETaxaCentavos(valor, 0m);

        var service = new PaymentIntentService();
        var options = new PaymentIntentCreateOptions
        {
            Amount = valorCentavos,
            Currency = "brl",
            PaymentMethodTypes = ["pix"],
            PaymentMethodOptions = new PaymentIntentPaymentMethodOptionsOptions
            {
                Pix = new PaymentIntentPaymentMethodOptionsPixOptions
                {
                    ExpiresAfterSeconds = 3600,
                },
            },
            Metadata = new Dictionary<string, string>
            {
                ["pagamento_treinador_id"] = pagamentoTreinadorId.ToString(),
                ["tipo"] = "plano_treinador",
            },
        };

        var intent = await service.CreateAsync(options, requestOptions: PagamentoTreinadorRequestOptions(pagamentoTreinadorId), cancellationToken: cancellationToken).ConfigureAwait(false);

        var pix = intent.NextAction?.PixDisplayQrCode
            ?? throw new InvalidOperationException("Stripe não retornou dados Pix.");

        var expiracao = timeProvider.GetUtcNow().UtcDateTime.AddSeconds(3600);

        logger.LogInformation("PaymentIntent Pix plano-treinador {IntentId} criado.", intent.Id);

        return new PixPaymentResult(intent.Id, pix.Data, pix.ImageUrlPng, expiracao);
    }

    public async Task<CartaoPaymentResult> CriarCartaoPlataformaPaymentIntentAsync(
        decimal valor,
        Guid pagamentoTreinadorId,
        CancellationToken cancellationToken = default)
    {
        var (valorCentavos, _) = MoneyCentavos.ValorETaxaCentavos(valor, 0m);

        var service = new PaymentIntentService();
        var options = new PaymentIntentCreateOptions
        {
            Amount = valorCentavos,
            Currency = "brl",
            PaymentMethodTypes = ["card"],
            Metadata = new Dictionary<string, string>
            {
                ["pagamento_treinador_id"] = pagamentoTreinadorId.ToString(),
                ["tipo"] = "plano_treinador",
            },
        };

        var intent = await service.CreateAsync(options, requestOptions: PagamentoTreinadorRequestOptions(pagamentoTreinadorId), cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("PaymentIntent Cartão plano-treinador {IntentId} criado.", intent.Id);

        return new CartaoPaymentResult(intent.Id, intent.ClientSecret);
    }

    public async Task<bool> ContaEstaAtivadaAsync(string stripeAccountId, CancellationToken cancellationToken = default)
    {
        var service = new AccountService();
        var account = await service.GetAsync(stripeAccountId, requestOptions: RequestOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return account.ChargesEnabled;
    }

    public async Task CriarReembolsoAsync(string paymentIntentId, bool reverterTransferencia, CancellationToken cancellationToken = default)
    {
        var service = new RefundService();
        var options = new RefundCreateOptions { PaymentIntent = paymentIntentId };

        if (reverterTransferencia)
        {
            options.ReverseTransfer = true;
            options.RefundApplicationFee = true;
        }

        var refund = await service.CreateAsync(options, requestOptions: RequestOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Reembolso {RefundId} criado para PaymentIntent {PaymentIntentId} (reverterTransferencia={Reverter}).",
            refund.Id, paymentIntentId, reverterTransferencia);
    }

    public async Task CancelarPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        var service = new PaymentIntentService();
        var intent = await service.CancelAsync(paymentIntentId, requestOptions: RequestOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        logger.LogInformation("PaymentIntent {PaymentIntentId} cancelado (status={Status}).", paymentIntentId, intent.Status);
    }

    public async Task EnviarEvidenciaDisputaAsync(string disputeId, DisputaEvidencia evidencias, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidencias);

        var evidence = new DisputeEvidenceOptions
        {
            CustomerEmailAddress = evidencias.EmailCliente,
            ServiceDate = evidencias.DataAtivacao?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            UncategorizedText = MontarTextoEvidencia(evidencias),
        };

        var service = new DisputeService();
        await service.UpdateAsync(
            disputeId,
            new DisputeUpdateOptions { Evidence = evidence },
            requestOptions: RequestOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Evidência de disputa enviada para dispute {DisputeId}.", disputeId);
    }

    private static string MontarTextoEvidencia(DisputaEvidencia e)
    {
        var partes = new List<string>();
        if (e.DataAtivacao is { } ativacao)
            partes.Add($"Serviço ativado em {ativacao:yyyy-MM-dd}.");
        if (e.DataUltimaAtividade is { } atividade)
            partes.Add($"Última atividade do cliente em {atividade:yyyy-MM-dd}.");
        if (e.DataUltimoPagamento is { } pagamento)
            partes.Add($"Último pagamento confirmado em {pagamento:yyyy-MM-dd}.");
        return string.Join(" ", partes);
    }

    public Task<bool> ValidarWebhookAsync(string payload, string assinaturaStripe)
    {
        try
        {
            EventUtility.ConstructEvent(payload, assinaturaStripe, _settings.WebhookSecret);
            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is StripeException or InvalidDataException or ArgumentException)
        {
            logger.LogWarning(ex, "Webhook Stripe inválido: {Message}.", ex.Message);
            return Task.FromResult(false);
        }
    }

    // Teto duro: 1000 eventos por execução. Janela default = 7d, com volume normal de webhooks
    // pagáveis por treinador isso é folgado; passa de 1000 e algo está muito errado (ou janela
    // foi pedida grande demais) — preferimos truncar e logar a perder controle.
    private const int MaxEventosPorExecucao = 1000;

    private static readonly string[] TiposReconciliaveis =
    [
        "payment_intent.succeeded",
        "payment_intent.payment_failed",
        "payment_intent.canceled",
        "account.updated",
        "charge.refunded",
        "charge.dispute.created",
    ];

    public async Task<IReadOnlyList<StripeEventSummary>> ListarEventosDesdeAsync(
        DateTime desdeUtc,
        CancellationToken cancellationToken = default)
    {
        var service = new EventService();
        var options = new EventListOptions
        {
            // Created.GreaterThanOrEqual aceita DateTime UTC; Stripe.net converte pra unix internally.
            Created = new DateRangeOptions
            {
                GreaterThanOrEqual = DateTime.SpecifyKind(desdeUtc, DateTimeKind.Utc),
            },
            Types = [.. TiposReconciliaveis],
            Limit = 100,
        };

        var coletados = new List<StripeEventSummary>();

        // ListAutoPagingAsync resolve cursor pagination transparentemente.
        // Stripe.net default page size = 100; cap externo em MaxEventosPorExecucao.
        await foreach (var evt in service.ListAutoPagingAsync(options, RequestOptions, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            coletados.Add(new StripeEventSummary(
                EventId: evt.Id,
                Type: evt.Type,
                PayloadRaw: evt.ToJson(),
                Created: DateTime.SpecifyKind(evt.Created, DateTimeKind.Utc)));

            if (coletados.Count >= MaxEventosPorExecucao)
            {
                logger.LogWarning(
                    "Reconciliação Stripe atingiu teto de {Max} eventos (desde {Desde:o}). Truncando varredura.",
                    MaxEventosPorExecucao, desdeUtc);
                break;
            }
        }

        // Stripe retorna em ordem DESC; reordena ASC para replays seguirem cronologia natural.
        coletados.Sort((a, b) => a.Created.CompareTo(b.Created));

        logger.LogInformation(
            "Stripe Events.List retornou {Count} eventos relevantes desde {Desde:o}.",
            coletados.Count, desdeUtc);

        return coletados;
    }
}
