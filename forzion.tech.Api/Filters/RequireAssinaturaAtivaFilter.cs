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

        var alunoRepository = services.GetRequiredService<IAlunoRepository>();
        var aluno = await alunoRepository
            .ObterPorContaIdAsync(userContext.ContaId, ct)
            .ConfigureAwait(false);

        if (aluno is null)
            return false;

        var assinaturaRepository = services.GetRequiredService<IAssinaturaAlunoRepository>();
        var assinaturaAtual = await assinaturaRepository
            .ObterAtualPorAlunoAsync(aluno.Id, ct)
            .ConfigureAwait(false);

        return assinaturaAtual?.Status == AssinaturaAlunoStatus.Inadimplente;
    }
}
