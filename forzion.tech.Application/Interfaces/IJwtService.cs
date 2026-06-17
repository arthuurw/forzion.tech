using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces;

/// <summary>Token de escopo restrito (MFA pendente / step-up): porta <c>jti</c> e expiração curta.</summary>
public record TokenEscopo(string Token, Guid Jti, DateTime ExpiraEm);

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

    /// <summary>
    /// Gera um JWT de escopo restrito (<c>scope</c>) sem as claims de negócio (<c>tipo_conta</c>/<c>perfil_id</c>),
    /// de modo que NÃO satisfaz as policies de papel nem a policy padrão (autenticado sem escopo).
    /// </summary>
    /// <param name="conta">A conta dona do token.</param>
    /// <param name="escopo">Escopo do token (<c>mfa_pending</c> ou <c>step_up</c>).</param>
    /// <param name="validade">Janela de validade curta a partir de agora.</param>
    TokenEscopo GerarTokenEscopo(Conta conta, string escopo, TimeSpan validade);
}
