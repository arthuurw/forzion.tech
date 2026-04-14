using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Usuarios.ObterUsuarioAtual;

public record ObterUsuarioAtualResponse(
    Guid UsuarioId,
    string Nome,
    string Email,
    Role Role,
    UsuarioStatus Status,
    Guid TenantId,
    string TenantNome,
    string? FotoUrl,
    string? Bio,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
