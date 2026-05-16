using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;

namespace forzion.tech.Api.Endpoints.Pagamentos;

public static class WebhookEndpoints
{
    private const int MaxWebhookBodyBytes = 65_536; // 64 KB — muito além do tamanho real dos eventos Stripe

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/webhooks/stripe", async (
            HttpContext httpContext,
            [Microsoft.AspNetCore.Mvc.FromServices] ProcessarWebhookStripeHandler handler,
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
                return Results.BadRequest("Payload excede o tamanho máximo permitido.");
            }

            var assinatura = httpContext.Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

            var result = await handler.HandleAsync(
                new ProcessarWebhookStripeCommand(payload, assinatura), cancellationToken).ConfigureAwait(false);

            return result.IsSuccess ? Results.Ok() : Results.BadRequest("Webhook inválido.");
        })
        .WithTags("Webhooks")
        .WithSummary("Recebe eventos do Stripe via webhook")
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
