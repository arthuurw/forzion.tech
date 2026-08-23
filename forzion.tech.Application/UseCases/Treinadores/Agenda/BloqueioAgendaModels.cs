using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.Agenda;

public record CriarBloqueioAgendaCommand(
    Guid TreinadorId,
    TipoBloqueio Tipo,
    DateTime? InicioUtc,
    DateTime? FimUtc,
    int? DiaSemana,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFim,
    string? Motivo);

public record BloqueioAgendaResponse(
    Guid Id,
    TipoBloqueio Tipo,
    DateTime? InicioUtc,
    DateTime? FimUtc,
    int? DiaSemana,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFim,
    string? Motivo,
    DateTime CreatedAt);
