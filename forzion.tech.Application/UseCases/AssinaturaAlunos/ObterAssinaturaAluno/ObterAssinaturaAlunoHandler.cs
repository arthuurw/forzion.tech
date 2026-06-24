using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.AssinaturaAlunos;

namespace forzion.tech.Application.UseCases.AssinaturaAlunos.ObterAssinaturaAluno;

public class ObterAssinaturaAlunoHandler(IAssinaturaAlunoRepository assinaturaRepository)
{
    public virtual async Task<AssinaturaAlunoResponse?> HandleAsync(
        Guid alunoId,
        CancellationToken cancellationToken = default)
    {
        var ativa = await assinaturaRepository.ObterAtualPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);

        return ativa is null ? null : AssinaturaAlunoResponseExtensions.ToResponse(ativa);
    }
}
