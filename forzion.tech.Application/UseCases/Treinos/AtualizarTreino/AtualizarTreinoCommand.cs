using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinos.AtualizarTreino;

public record AtualizarTreinoCommand(
    Guid TreinoId,
    string? Nome,
    ObjetivoTreino? Objetivo,
    DificuldadeTreino? Dificuldade = null,
    DateOnly? DataInicio = null,
    DateOnly? DataFim = null,
    bool LimparDataInicio = false,
    bool LimparDataFim = false);
