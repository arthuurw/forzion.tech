namespace forzion.tech.Application.UseCases.Pacotes.AtualizarPacoteAluno;

public record AtualizarPacoteAlunoCommand(Guid TreinadorId, Guid PacoteId, string? Nome, decimal? Preco, string? Descricao);
