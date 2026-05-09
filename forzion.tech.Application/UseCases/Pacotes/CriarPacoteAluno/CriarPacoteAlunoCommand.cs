namespace forzion.tech.Application.UseCases.Pacotes.CriarPacoteAluno;

public record CriarPacoteAlunoCommand(Guid TreinadorId, string Nome, decimal Preco, string? Descricao = null);
