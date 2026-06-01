using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Api.Filters;

/// <summary>
/// Bloqueia endpoints de "consumo" do aluno quando sua assinatura atual está
/// Inadimplente. Retorna 403 com code <c>ASSINATURA_INADIMPLENTE</c>.
/// Tipos de conta que não sejam <see cref="TipoConta.Aluno"/> passam direto
/// (somente fluxos de consumo do aluno são bloqueados). GET endpoints (leitura)
/// permanecem liberados — bloquear apenas escrita preserva visibilidade LGPD.
/// </summary>
public sealed class RequireAssinaturaAtivaFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var userContext = services.GetRequiredService<IUserContext>();

        if (userContext.TipoConta != TipoConta.Aluno)
            return await next(context);

        var alunoRepository = services.GetRequiredService<IAlunoRepository>();
        var aluno = await alunoRepository
            .ObterPorContaIdAsync(userContext.ContaId, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (aluno is null)
            return await next(context);

        var assinaturaRepository = services.GetRequiredService<IAssinaturaAlunoRepository>();
        var assinatura = await assinaturaRepository
            .ObterAtualPorAlunoAsync(aluno.Id, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (assinatura?.Status == AssinaturaAlunoStatus.Inadimplente)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Assinatura inadimplente",
                detail: "Regularize seu pagamento para continuar usando esta funcionalidade.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "ASSINATURA_INADIMPLENTE"
                });
        }

        return await next(context);
    }
}
