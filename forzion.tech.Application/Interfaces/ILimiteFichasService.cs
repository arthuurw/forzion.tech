namespace forzion.tech.Application.Interfaces;

public interface ILimiteFichasService
{
    /// <summary>
    /// Lança <see cref="forzion.tech.Domain.Exceptions.LimiteFichasAtingidoException"/> se o aluno
    /// atingiu o MaxFichas do PacoteAluno do vínculo ativo.
    /// </summary>
    Task ValidarAsync(Guid alunoId, CancellationToken cancellationToken = default);
}
