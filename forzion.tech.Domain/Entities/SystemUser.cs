using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class SystemUser
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public SystemRole Role { get; private set; }
    public UsuarioStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private SystemUser() { }

    public static Result<SystemUser> Criar(Guid contaId, string nome, DateTime agora, SystemRole role = SystemRole.SuperAdmin)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<SystemUser>(SystemUserErrors.ContaIdInvalido);
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure<SystemUser>(SystemUserErrors.NomeObrigatorio);
        if (nome.Trim().Length > 100)
            return Result.Failure<SystemUser>(SystemUserErrors.NomeMuitoLongo);

        return Result.Success(new SystemUser
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            Nome = nome.Trim(),
            Role = role,
            Status = UsuarioStatus.Ativo,
            CreatedAt = agora
        });
    }

    public void AlterarRole(SystemRole novoRole, DateTime agora)
    {
        Role = novoRole;
        UpdatedAt = agora;
    }

    public void AlterarStatus(UsuarioStatus novoStatus, DateTime agora)
    {
        Status = novoStatus;
        UpdatedAt = agora;
    }

    public Result AtualizarNome(string nome, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure(SystemUserErrors.NomeObrigatorio);
        if (nome.Trim().Length > 100)
            return Result.Failure(SystemUserErrors.NomeMuitoLongo);

        Nome = nome.Trim();
        UpdatedAt = agora;
        return Result.Success();
    }
}
