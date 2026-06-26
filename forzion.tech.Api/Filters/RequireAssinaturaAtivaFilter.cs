using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Api.Filters;

public sealed class RequireAssinaturaAtivaFilter : RequireAssinaturaAtivaFilterBase
{
    protected override string CodigoErro => "ASSINATURA_INADIMPLENTE";

    protected override async Task<bool> EstaInadimplenteAsync(
        IServiceProvider services,
        IUserContext userContext,
        CancellationToken ct)
    {
        if (userContext.TipoConta != TipoConta.Aluno)
            return false;

        var assinaturaRepository = services.GetRequiredService<IAssinaturaAlunoRepository>();
        return await assinaturaRepository
            .AlunoEstaInadimplentePorContaIdAsync(userContext.ContaId, ct)
            .ConfigureAwait(false);
    }
}
