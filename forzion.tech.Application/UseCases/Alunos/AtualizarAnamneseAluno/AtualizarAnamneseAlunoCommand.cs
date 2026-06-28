using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Alunos.AtualizarAnamneseAluno;

public record AtualizarAnamneseAlunoCommand(
    Guid AlunoId,
    int? DiasDisponiveis = null,
    int? TempoDisponivelMinutos = null,
    FinalidadeTreino? Finalidade = null,
    string? FocoTreino = null,
    NivelCondicionamento? NivelCondicionamento = null,
    string? LimitacoesFisicas = null,
    string? Doencas = null,
    string? ObservacoesAdicionais = null,
    bool ConsentimentoDadosSaude = false,
    DateTime? ConsentimentoDadosSaudeEm = null)
{
    public bool ColetaDadosSaude =>
        Finalidade is not null
        || NivelCondicionamento is not null
        || !string.IsNullOrWhiteSpace(FocoTreino)
        || !string.IsNullOrWhiteSpace(LimitacoesFisicas)
        || !string.IsNullOrWhiteSpace(Doencas)
        || !string.IsNullOrWhiteSpace(ObservacoesAdicionais);
}
