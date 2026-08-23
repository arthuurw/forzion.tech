namespace forzion.tech.Application.UseCases.Treinadores.Agenda;

public record AtualizarPoliticaAgendaCommand(Guid TreinadorId, int AntecedenciaMinimaHoras, int HorizonteDias);

public record PoliticaAgendaResponse(int AntecedenciaMinimaHoras, int HorizonteDias);
