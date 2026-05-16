using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Assinaturas;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Assinaturas.ObterAssinaturaAluno;

public class ObterAssinaturaAlunoHandler(IAssinaturaRepository assinaturaRepository)
{
    public virtual async Task<AssinaturaResponse?> HandleAsync(
        Guid alunoId,
        CancellationToken cancellationToken = default)
    {
        var assinaturas = await assinaturaRepository.ListarPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);

        var ativa = assinaturas
            .Where(a => a.Status != AssinaturaStatus.Cancelada)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        return ativa is null ? null : AssinaturaResponseExtensions.ToResponse(ativa);
    }
}
