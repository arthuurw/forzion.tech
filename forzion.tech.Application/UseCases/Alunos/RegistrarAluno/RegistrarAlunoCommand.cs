using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Alunos.RegistrarAluno;

public record RegistrarAlunoCommand(
    string Email,
    string Senha,
    string Nome,
    Guid TreinadorId,
    Guid PacoteId,
    string? Telefone = null,
    int? DiasDisponiveis = null,
    int? TempoDisponivelMinutos = null,
    FinalidadeTreino? Finalidade = null,
    string? FocoTreino = null,
    NivelCondicionamento? NivelCondicionamento = null,
    string? LimitacoesFisicas = null,
    string? Doencas = null,
    string? ObservacoesAdicionais = null);
