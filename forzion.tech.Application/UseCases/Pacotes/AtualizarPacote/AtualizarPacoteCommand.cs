namespace forzion.tech.Application.UseCases.Pacotes.AtualizarPacote;

public record AtualizarPacoteCommand(
    Guid TreinadorId,
    Guid PacoteId,
    string? Nome,
    decimal? Preco,
    string? Descricao,
    string? Categoria = null,
    int? DuracaoMinutos = null,
    bool? TrialDisponivel = null,
    bool? IsPublico = null);
