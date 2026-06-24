using System.Security.Cryptography;
using System.Text;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Api.Endpoints.Pagamentos;

public static class WebhookEndpoints
{
    private const int MaxWebhookBodyBytes = 65_536; // 64 KB — muito além do tamanho real dos eventos Stripe

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/webhooks/stripe", async (
            HttpContext httpContext,
            [Microsoft.AspNetCore.Mvc.FromServices] ProcessarWebhookStripeHandler handler,
            [Microsoft.AspNetCore.Mvc.FromServices] ILogger<ProcessarWebhookStripeHandler> logger,
            CancellationToken cancellationToken) =>
        {
            // Limita tamanho do body para prevenir DoS
            httpContext.Request.Body = new LimitedStream(httpContext.Request.Body, MaxWebhookBodyBytes);

            using var reader = new StreamReader(httpContext.Request.Body);
            string payload;
            try
            {
                payload = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                return Results.Problem(detail: "Payload excede o tamanho máximo permitido.", statusCode: 400);
            }

            var assinatura = httpContext.Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

            var result = await handler.HandleAsync(
                new ProcessarWebhookStripeCommand(payload, assinatura), cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess) return Results.Ok();
            logger.LogWarning("Webhook Stripe rejeitado: {Motivo}", result.Error?.Message);
            return Results.Problem(detail: "Webhook inválido.", statusCode: 400);
        })
        .WithTags("Webhooks")
        .WithSummary("Recebe eventos do Stripe via webhook")
        .AllowAnonymous()
        .RequireRateLimiting("webhook")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        endpoints.MapPost("/webhooks/resend", async (
            HttpContext httpContext,
            [FromServices] ProcessarWebhookResendHandler handler,
            [FromServices] IConfiguration configuration,
            [FromServices] ILogger<ProcessarWebhookResendHandler> logger,
            CancellationToken cancellationToken) =>
        {
            httpContext.Request.Body = new LimitedStream(httpContext.Request.Body, MaxWebhookBodyBytes);

            using var reader = new StreamReader(httpContext.Request.Body);
            string payload;
            try
            {
                payload = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                return Results.Problem(detail: "Payload excede o tamanho máximo permitido.", statusCode: 400);
            }

            var svixId = httpContext.Request.Headers["svix-id"].FirstOrDefault() ?? string.Empty;
            var svixTimestamp = httpContext.Request.Headers["svix-timestamp"].FirstOrDefault() ?? string.Empty;
            var svixSignature = httpContext.Request.Headers["svix-signature"].FirstOrDefault() ?? string.Empty;
            var webhookSecret = configuration["Resend:WebhookSecret"] ?? string.Empty;

            var result = await handler.HandleAsync(
                new ProcessarWebhookResendCommand(payload, svixId, svixTimestamp, svixSignature),
                webhookSecret,
                cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess) return Results.Ok();
            logger.LogWarning("Webhook Resend rejeitado: {Motivo}", result.Error?.Message);
            return Results.Problem(detail: "Webhook inválido.", statusCode: 400);
        })
        .WithTags("Webhooks")
        .WithSummary("Recebe eventos de entrega de e-mail via Resend/Svix webhook")
        .AllowAnonymous()
        .RequireRateLimiting("webhook")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // GET /webhooks/whatsapp — Meta verification handshake
        endpoints.MapGet("/webhooks/whatsapp", (
            HttpContext httpContext,
            [FromServices] IConfiguration configuration) =>
        {
            var mode = httpContext.Request.Query["hub.mode"].FirstOrDefault();
            var verifyToken = httpContext.Request.Query["hub.verify_token"].FirstOrDefault() ?? string.Empty;
            var challenge = httpContext.Request.Query["hub.challenge"].FirstOrDefault();

            var expectedToken = configuration["WhatsApp:WebhookVerifyToken"] ?? string.Empty;

            // Constant-time comparison to prevent timing-based token oracle attacks.
            var verifyTokenBytes = Encoding.UTF8.GetBytes(verifyToken);
            var expectedTokenBytes = Encoding.UTF8.GetBytes(expectedToken);
            var tokensMatch = verifyTokenBytes.Length == expectedTokenBytes.Length
                && CryptographicOperations.FixedTimeEquals(verifyTokenBytes, expectedTokenBytes);

            if (mode == "subscribe" && tokensMatch)
                return Results.Text(challenge ?? string.Empty);

            return Results.Forbid();
        })
        .WithTags("Webhooks")
        .WithSummary("Handshake de verificação do webhook WhatsApp (Meta)")
        .AllowAnonymous()
        .RequireRateLimiting("webhook")
        .Produces<string>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden);

        // POST /webhooks/whatsapp — delivery-status events
        endpoints.MapPost("/webhooks/whatsapp", async (
            HttpContext httpContext,
            [FromServices] ProcessarWebhookWhatsAppHandler handler,
            [FromServices] IConfiguration configuration,
            [FromServices] ILogger<ProcessarWebhookWhatsAppHandler> logger,
            CancellationToken cancellationToken) =>
        {
            httpContext.Request.Body = new LimitedStream(httpContext.Request.Body, MaxWebhookBodyBytes);

            using var reader = new StreamReader(httpContext.Request.Body);
            string payload;
            try
            {
                payload = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                return Results.Problem(detail: "Payload excede o tamanho máximo permitido.", statusCode: 400);
            }

            var signature = httpContext.Request.Headers["X-Hub-Signature-256"].FirstOrDefault() ?? string.Empty;
            var appSecret = configuration["WhatsApp:AppSecret"] ?? string.Empty;

            var result = await handler.HandleAsync(
                new ProcessarWebhookWhatsAppCommand(payload, signature),
                appSecret,
                cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess) return Results.Ok();
            logger.LogWarning("Webhook WhatsApp rejeitado: {Motivo}", result.Error?.Message);
            return Results.Problem(detail: "Webhook inválido.", statusCode: 400);
        })
        .WithTags("Webhooks")
        .WithSummary("Recebe eventos de entrega de WhatsApp via Meta Cloud API webhook")
        .AllowAnonymous()
        .RequireRateLimiting("webhook")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}

/// <summary>
/// Stream wrapper que lança InvalidDataException ao atingir o limite de bytes.
/// </summary>
internal sealed class LimitedStream(Stream inner, long maxBytes) : Stream
{
    private long _bytesRead;

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = maxBytes - _bytesRead;
        if (remaining <= 0) throw new InvalidDataException("Payload excede o limite máximo.");
        var toRead = (int)Math.Min(count, remaining);
        var read = inner.Read(buffer, offset, toRead);
        _bytesRead += read;
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var remaining = maxBytes - _bytesRead;
        if (remaining <= 0) throw new InvalidDataException("Payload excede o limite máximo.");
        var toRead = (int)Math.Min(count, remaining);
        var read = await inner.ReadAsync(buffer.AsMemory(offset, toRead), cancellationToken).ConfigureAwait(false);
        _bytesRead += read;
        return read;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
