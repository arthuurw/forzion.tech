namespace forzion.tech.Application.UseCases.Exercicios.ListarExercicios;

public record ListarExerciciosQuery(
    Guid? TreinadorId,
    int Pagina,
    int TamanhoPagina,
    string? Nome = null,
    Guid? GrupoMuscularId = null,
    string OrdenarPor = "nome");
