using forzion.tech.Application.UseCases.Exercicios;

namespace forzion.tech.Application.UseCases.Exercicios.ListarExercicios;

public record ListarExerciciosResponse(
    IReadOnlyList<ExercicioResponse> Items,
    int Total,
    int Pagina,
    int TamanhoPagina);
