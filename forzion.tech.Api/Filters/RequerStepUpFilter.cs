using forzion.tech.Application.Auth;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Api.Filters;

public sealed class RequerStepUpFilter : IEndpointFilter
{
    public const string Header = "X-Step-Up-Token";
    public const string CodigoErro = "step_up_requerido";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;
        var logger = services.GetRequiredService<ILogger<RequerStepUpFilter>>();
        var path = http.Request.Path.Value;

        var token = http.Request.Headers[Header].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Step-up recusado em {Path}: header {Header} ausente.", path, Header);
            return StepUpRequerido();
        }

        var validado = services.GetRequiredService<IJwtService>().ValidarTokenEscopo(token, MfaScopes.StepUp);
        var userContext = services.GetRequiredService<IUserContext>();
        if (validado is null)
        {
            logger.LogWarning("Step-up recusado em {Path}: token inválido, expirado ou de escopo divergente.", path);
            return StepUpRequerido();
        }
        if (validado.ContaId != userContext.ContaId)
        {
            logger.LogWarning(
                "Step-up recusado em {Path}: conta do token {TokenContaId} difere da conta autenticada {ContaId}.",
                path, validado.ContaId, userContext.ContaId);
            return StepUpRequerido();
        }

        var revogados = services.GetRequiredService<ITokenRevogadoRepository>();
        if (await revogados.EstaRevogadoAsync(validado.Jti, http.RequestAborted).ConfigureAwait(false))
        {
            logger.LogWarning("Step-up recusado em {Path}: token revogado (jti {Jti}).", path, validado.Jti);
            return StepUpRequerido();
        }

        return await next(context);
    }

    private static IResult StepUpRequerido() =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Reautenticação necessária",
            detail: "Esta ação exige verificação adicional (step-up).",
            extensions: new Dictionary<string, object?> { ["code"] = CodigoErro });
}
