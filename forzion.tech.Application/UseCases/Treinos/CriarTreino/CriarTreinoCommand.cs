using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinos.CriarTreino;

public record CriarTreinoCommand(
    Guid TreinadorId,
    Guid? AlunoId,
    string Nome,
    ObjetivoTreino Objetivo,
    DificuldadeTreino Dificuldade = DificuldadeTreino.Iniciante,
    DateOnly? DataInicio = null,
    DateOnly? DataFim = null);
