using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinos.CriarTreino;

public record CriarTreinoCommand(
    Guid TenantId,
    Guid TreinadorId,
    Guid AlunoId,
    string Nome,
    ObjetivoTreino Objetivo);
