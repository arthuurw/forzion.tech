using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces;

public interface IUserContext
{
    Guid ContaId { get; }
    TipoConta TipoConta { get; }

    /// <summary>
    /// Id do perfil do usuário autenticado (SystemUser.Id, Treinador.Id ou Aluno.Id).
    /// </summary>
    Guid PerfilId { get; }

    Guid Jti { get; }
    DateTime TokenExpiraEm { get; }

    /// <summary>Família de refresh da sessão (claim <c>fam</c>). <see cref="Guid.Empty"/> em tokens sem a claim.</summary>
    Guid FamiliaId => Guid.Empty;

    bool IsSystemAdmin => TipoConta == TipoConta.SystemAdmin;
    bool IsTreinador => TipoConta == TipoConta.Treinador;
    bool IsAluno => TipoConta == TipoConta.Aluno;
}
