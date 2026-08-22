namespace forzion.tech.Application.UseCases.Pacotes.CriarPacote;

public record CriarPacoteCommand(
    Guid TreinadorId,
    string Nome,
    decimal Preco,
    string? Descricao = null,
    string? Categoria = null,
    int? DuracaoMinutos = null,
    bool TrialDisponivel = false,
    bool IsPublico = false);
