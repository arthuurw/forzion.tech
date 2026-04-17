namespace forzion.tech.Application.UseCases.Treinadores.AtribuirPlano;

public record AtribuirPlanoCommand(Guid TreinadorId, Guid PlanoId, Guid AdminId);
