using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Alunos;

public record AlunoResponse(
    Guid AlunoId,
    string Nome,
    string? Email,
    string? Telefone,
    AlunoStatus Status,
    Guid TenantId,
    Guid TreinadorId,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
