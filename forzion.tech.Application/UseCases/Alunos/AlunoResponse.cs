using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Alunos;

public record AlunoResponse(
    Guid AlunoId,
    string Nome,
    string? Email,
    string? Telefone,
    AlunoStatus Status,
    Guid ContaId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int? DiasDisponiveis = null,
    TempoDisponivel? TempoDisponivelMinutos = null,
    FinalidadeTreino? Finalidade = null,
    string? FocoTreino = null,
    NivelCondicionamento? NivelCondicionamento = null,
    string? LimitacoesFisicas = null,
    string? Doencas = null,
    string? ObservacoesAdicionais = null,
    Guid? PacoteId = null,
    string? PacoteNome = null
);
