namespace forzion.tech.Application.UseCases.Pacotes.AtualizarPacote;

public record AtualizarPacoteCommand(Guid TreinadorId, Guid PacoteId, string? Nome, decimal? Preco, string? Descricao);
