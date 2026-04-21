namespace forzion.tech.Application.UseCases.Vinculos.SolicitarTrocaTreinador;

public record SolicitarTrocaTreinadorCommand(Guid AlunoId, Guid NovoTreinadorId, Guid PacoteId);
