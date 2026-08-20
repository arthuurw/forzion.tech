using forzion.tech.Api.Endpoints.Pagamentos;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Api.Endpoints.Agents.Hmac;

internal sealed class HmacSignatureFilter(
    HmacSignatureVerifier verificador,
    ILogger<HmacSignatureFilter> logger) : IEndpointFilter
{
    internal const string HeaderDeAssinatura = "X-Forzion-Signature";
    internal const string HeaderDeTimestamp = "X-Forzion-Timestamp";

    private const int MaxBodyBytes = 65_536;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var requisicao = context.HttpContext.Request;

        MemoryStream corpo;
        try
        {
            corpo = await LerCorpoAsync(requisicao, context.HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return Rejeitar(requisicao, AgentErrorCode.ValidationFailed, StatusCodes.Status400BadRequest);
        }

        var resultado = verificador.Verificar(
            requisicao.Method,
            MontarCaminhoComQuery(context.HttpContext),
            corpo.GetBuffer().AsSpan(0, (int)corpo.Length),
            requisicao.Headers[HeaderDeAssinatura].FirstOrDefault(),
            requisicao.Headers[HeaderDeTimestamp].FirstOrDefault());

        if (resultado is not HmacVerificationResult.Valida)
        {
            var code = resultado is HmacVerificationResult.TimestampForaDaJanela
                ? AgentErrorCode.TimestampOutOfWindow
                : AgentErrorCode.SignatureInvalid;
            return Rejeitar(requisicao, code, StatusCodes.Status401Unauthorized);
        }

        // O handler recebe o MESMO buffer que foi hasheado, não uma releitura do socket.
        requisicao.Body = corpo;

        return await next(context).ConfigureAwait(false);
    }

    // RawTarget carrega os bytes crus da wire; o operador `PathString + QueryString` passa por
    // ToUriComponent() nos dois lados e re-normaliza o percent-encoding (`%7E` vira `~`), gerando
    // payload diferente do que o gateway assinou — 401 mudo. Host que não popula RawTarget
    // (TestServer) cai no fallback, que concatena os `.Value` sem passar pelo operador.
    private static string MontarCaminhoComQuery(HttpContext contexto)
    {
        var alvoCru = contexto.Features.Get<IHttpRequestFeature>()?.RawTarget;

        return string.IsNullOrEmpty(alvoCru)
            ? contexto.Request.Path.Value + contexto.Request.QueryString.Value
            : alvoCru;
    }

    private static async Task<MemoryStream> LerCorpoAsync(HttpRequest requisicao, CancellationToken cancellationToken)
    {
        var limitado = new LimitedStream(requisicao.Body, MaxBodyBytes);
        var buffer = new MemoryStream();

        await limitado.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;

        return buffer;
    }

    private IResult Rejeitar(HttpRequest requisicao, AgentErrorCode code, int status)
    {
        // O motivo da rejeição só existe aqui: o chamador recebe `detail` fixo. Sem este log a
        // divergência de payload canônico entre os dois lados fica indiagnosticável.
        logger.LogWarning(
            "Requisicao ao grupo de agentes rejeitada — Motivo: {Motivo} Metodo: {Metodo} Caminho: {Caminho}",
            code.ValorDeWire(),
            requisicao.Method,
            requisicao.Path.Value);

        return AgentProblem.Criar(code, status);
    }
}
