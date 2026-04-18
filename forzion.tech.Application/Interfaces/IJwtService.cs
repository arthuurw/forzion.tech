using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces;

public interface IJwtService
{
    /// <summary>
    /// Gera um JWT para a conta autenticada.
    /// </summary>
    /// <param name="conta">A conta autenticada.</param>
    /// <param name="perfilId">Id do perfil vinculado (Treinador.Id, Aluno.Id ou Conta.Id para SystemAdmin).</param>
    string GerarToken(Conta conta, Guid perfilId);
}
