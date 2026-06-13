using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces;

public interface IJwtService
{
    /// <summary>
    /// Gera um JWT para a conta autenticada.
    /// </summary>
    /// <param name="conta">A conta autenticada.</param>
    /// <param name="perfilId">Id do perfil vinculado (Treinador.Id, Aluno.Id ou Conta.Id para SystemAdmin).</param>
    /// <param name="nome">Nome do perfil — exposto como claim p/ a UI exibir o usuário logado sem round-trip extra.</param>
    /// <param name="familiaId">Id da família de refresh desta sessão — claim <c>fam</c> p/ o logout revogar só este device. <c>default</c> omite a claim.</param>
    string GerarToken(Conta conta, Guid perfilId, string nome, Guid familiaId = default);
}
