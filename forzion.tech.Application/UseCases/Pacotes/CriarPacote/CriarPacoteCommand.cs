namespace forzion.tech.Application.UseCases.Pacotes.CriarPacote;

public record CriarPacoteCommand(Guid TreinadorId, string Nome, decimal Preco, string? Descricao = null);
