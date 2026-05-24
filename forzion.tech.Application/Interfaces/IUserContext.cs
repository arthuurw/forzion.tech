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

    bool IsSystemAdmin => TipoConta == TipoConta.SystemAdmin;
    bool IsTreinador => TipoConta == TipoConta.Treinador;
    bool IsAluno => TipoConta == TipoConta.Aluno;
}
