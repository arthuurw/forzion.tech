using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Usuarios.RegistrarUsuario;

public record RegistrarUsuarioResponse(
    Guid UsuarioId,
    string Nome,
    string Email,
    Role Role,
    Guid TenantId,
    string TenantNome
);
