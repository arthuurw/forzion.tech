namespace forzion.tech.AI.GuardRails;

public sealed record SugestaoDraft(
    Guid TreinadorId,
    Guid AlunoId,
    string Objetivo,
    string Dificuldade,
    int NumeroDeTreinos,
    DateTime ExpiresAt);
