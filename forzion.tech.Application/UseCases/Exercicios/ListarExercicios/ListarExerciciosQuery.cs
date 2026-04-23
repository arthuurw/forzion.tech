using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Exercicios.ListarExercicios;

public record ListarExerciciosQuery(
    Guid? TreinadorId,
    int Pagina,
    int TamanhoPagina,
    string? Nome = null,
    GrupoMuscular? GrupoMuscular = null,
    string OrdenarPor = "nome");
