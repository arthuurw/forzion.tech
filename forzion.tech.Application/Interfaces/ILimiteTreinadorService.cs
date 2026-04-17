namespace forzion.tech.Application.Interfaces;

public interface ILimiteTreinadorService
{
    /// <summary>
    /// Lança <see cref="forzion.tech.Domain.Exceptions.LimiteAlunosAtingidoException"/> se o treinador
    /// atingiu o MaxAlunos do seu PlanoTreinador.
    /// </summary>
    Task ValidarAsync(Guid treinadorId, CancellationToken cancellationToken = default);
}
