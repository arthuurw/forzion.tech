using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Api.Filters;

public sealed class RequireAssinaturaTreinadorAtivaFilter : RequireAssinaturaAtivaFilterBase
{
    protected override string CodigoErro => "ASSINATURA_TREINADOR_INADIMPLENTE";

    protected override Task<bool> EstaInadimplenteAsync(
        IServiceProvider services,
        IUserContext userContext,
        CancellationToken ct)
    {
        if (userContext.TipoConta != TipoConta.Treinador)
            return Task.FromResult(false);

        // Quando billing do treinador for implementado, consultar IAssinaturaTreinadorRepository aqui.
        return Task.FromResult(false);
    }
}
