namespace forzion.tech.Application.UseCases.Treinos.AtualizarObservacaoExercicio;

public record AtualizarObservacaoExercicioCommand(
    Guid TreinoId,
    Guid TreinoExercicioId,
    string? Observacao);
