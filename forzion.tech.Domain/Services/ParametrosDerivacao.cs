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
    public const int MaxHorariosFuncionamento = 21;
    public const int MaxNomeFantasiaLength = 200;
    public const int MaxPoliticas = 20;
    public const int MaxPoliticaChaveLength = 100;
    public const int MaxPoliticaValorLength = 500;
}
