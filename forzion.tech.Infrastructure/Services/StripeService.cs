using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Pagamentos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace forzion.tech.Infrastructure.Services;

public class StripeService(
    IOptions<StripeSettings> settings,
    ILogger<StripeService> logger) : IStripeService
{
    private readonly StripeSettings _settings = settings.Value;

    // C4: Chave passada explicitamente em cada chamada — sem dependência de estado global
    private RequestOptions RequestOptions => new() { ApiKey = _settings.SecretKey };

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

        var intent = await service.CreateAsync(options, requestOptions: RequestOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

        var pix = intent.NextAction?.PixDisplayQrCode
            ?? throw new InvalidOperationException("Stripe não retornou dados Pix.");

        var expiracao = DateTime.UtcNow.AddSeconds(3600);

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

        var intent = await service.CreateAsync(options, requestOptions: RequestOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("PaymentIntent Cartão {IntentId} criado para conta {AccountId}.", intent.Id, stripeAccountId);

        return new CartaoPaymentResult(intent.Id, intent.ClientSecret);
    }

    public async Task<bool> ContaEstaAtivadaAsync(string stripeAccountId, CancellationToken cancellationToken = default)
    {
        var service = new AccountService();
        var account = await service.GetAsync(stripeAccountId, requestOptions: RequestOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return account.ChargesEnabled;
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
}
