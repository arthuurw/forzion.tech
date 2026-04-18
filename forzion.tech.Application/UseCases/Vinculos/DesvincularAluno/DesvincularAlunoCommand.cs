namespace forzion.tech.Application.UseCases.Vinculos.DesvincularAluno;

public record DesvincularAlunoCommand(Guid VinculoId, Guid RealizadoPorId, string? Observacao = null);
