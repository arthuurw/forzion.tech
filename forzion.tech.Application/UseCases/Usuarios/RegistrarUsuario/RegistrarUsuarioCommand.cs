namespace forzion.tech.Application.UseCases.Usuarios.RegistrarUsuario;

public record RegistrarUsuarioCommand(
    Guid SupabaseId,
    string Nome,
    string Email,
    string TenantNome
);
