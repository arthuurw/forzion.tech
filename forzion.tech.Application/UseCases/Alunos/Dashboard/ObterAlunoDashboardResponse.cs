using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Alunos.Dashboard;

public record FichaAtivaResumo(
    Guid TreinoAlunoId,
    Guid TreinoId,
    string NomeTreino,
    ObjetivoTreino Objetivo,
    DateTime CriadoEm);

public record SessaoSemanaItem(DateTime SemanaInicio, DateTime SemanaFim, int Total);

public record VinculoResumo(bool Ativo, bool Pendente);

public record ObterAlunoDashboardResponse(
    int TotalFichas,
    IReadOnlyList<FichaAtivaResumo> FichasAtivas,
    int TotalExecucoes,
    IReadOnlyList<SessaoSemanaItem> SessoesPorSemana,
    VinculoResumo Vinculo);
