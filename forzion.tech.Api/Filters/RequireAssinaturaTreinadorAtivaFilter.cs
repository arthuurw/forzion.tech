using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Api.Filters;

public sealed class RequireAssinaturaTreinadorAtivaFilter : RequireAssinaturaAtivaFilterBase
{
    protected override string CodigoErro => "ASSINATURA_TREINADOR_INADIMPLENTE";

    protected override async Task<bool> EstaInadimplenteAsync(
        IServiceProvider services,
        IUserContext userContext,
        CancellationToken ct)
    {
        if (userContext.TipoConta != TipoConta.Treinador)
            return false;

        var assinaturaRepository = services.GetRequiredService<IAssinaturaTreinadorRepository>();
        var assinaturaAtual = await assinaturaRepository
            .ObterAtualPorTreinadorAsync(userContext.PerfilId, ct)
            .ConfigureAwait(false);

        return assinaturaAtual?.Status == AssinaturaTreinadorStatus.Inadimplente;
    }
}
