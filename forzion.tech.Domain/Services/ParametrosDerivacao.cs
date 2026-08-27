using forzion.tech.Domain.Entities;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Services;

public sealed record ParametrosDerivacao(
    Guid TreinadorId,
    Guid PacoteId,
    int DuracaoMinutos,
    DateTime From,
    DateTime To,
    DateTime Agora,
    TimeZoneInfo Fuso,
    PoliticaAgenda Politica,
    IReadOnlyList<HorarioFuncionamento> Horarios,
    IReadOnlyList<BloqueioAgenda> Bloqueios)
{
    public const int MaxSlotsMaterializados = 10_000;
}
