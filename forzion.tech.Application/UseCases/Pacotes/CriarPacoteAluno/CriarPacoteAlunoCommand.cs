namespace forzion.tech.Application.UseCases.Pacotes.CriarPacoteAluno;

public record CriarPacoteAlunoCommand(Guid TreinadorId, string Nome, int MaxFichas, decimal Preco);
