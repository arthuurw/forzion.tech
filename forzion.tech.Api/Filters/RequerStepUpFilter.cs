using forzion.tech.Application.Auth;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Api.Filters;

public sealed class RequerStepUpFilter : IEndpointFilter
{
    public const string Header = "X-Step-Up-Token";
    public const string CodigoErro = "step_up_requerido";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;

        var token = http.Request.Headers[Header].ToString();
        if (string.IsNullOrWhiteSpace(token))
            return StepUpRequerido();

        var validado = services.GetRequiredService<IJwtService>().ValidarTokenEscopo(token, MfaScopes.StepUp);
        var userContext = services.GetRequiredService<IUserContext>();
        if (validado is null || validado.ContaId != userContext.ContaId)
            return StepUpRequerido();

        var revogados = services.GetRequiredService<ITokenRevogadoRepository>();
        if (await revogados.EstaRevogadoAsync(validado.Jti, http.RequestAborted).ConfigureAwait(false))
            return StepUpRequerido();

        return await next(context);
    }

    private static IResult StepUpRequerido() =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Reautenticação necessária",
            detail: "Esta ação exige verificação adicional (step-up).",
            extensions: new Dictionary<string, object?> { ["code"] = CodigoErro });
}
