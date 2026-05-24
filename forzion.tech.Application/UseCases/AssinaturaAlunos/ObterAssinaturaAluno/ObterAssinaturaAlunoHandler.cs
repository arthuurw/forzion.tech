using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.AssinaturaAlunos;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.AssinaturaAlunos.ObterAssinaturaAluno;

public class ObterAssinaturaAlunoHandler(IAssinaturaAlunoRepository assinaturaRepository)
{
    public virtual async Task<AssinaturaAlunoResponse?> HandleAsync(
        Guid alunoId,
        CancellationToken cancellationToken = default)
    {
        var assinaturas = await assinaturaRepository.ListarPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);

        var ativa = assinaturas
            .Where(a => a.Status != AssinaturaAlunoStatus.Cancelada)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        return ativa is null ? null : AssinaturaAlunoResponseExtensions.ToResponse(ativa);
    }
}
