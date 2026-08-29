namespace forzion.tech.Domain.Shared.Errors;

public static class PoliticaAgendaErrors
{
    public static Error AntecedenciaMinimaInvalida => Error.Validation("politica_agenda.antecedencia_minima_invalida", "A antecedência mínima deve estar entre 0 e 8760 horas (1 ano).");
    public static Error HorizonteInvalido => Error.Validation("politica_agenda.horizonte_invalido", "O horizonte de dias deve estar entre 1 e 365.");
}
