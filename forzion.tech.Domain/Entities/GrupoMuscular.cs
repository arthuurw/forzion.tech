using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class GrupoMuscular
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private GrupoMuscular() { }

    public static Result<GrupoMuscular> Criar(string nome, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure<GrupoMuscular>(GrupoMuscularErrors.NomeObrigatorio);

        if (nome.Trim().Length > 50)
            return Result.Failure<GrupoMuscular>(GrupoMuscularErrors.NomeMuitoLongo);

        return Result.Success(new GrupoMuscular
        {
            Id = Guid.NewGuid(),
            Nome = nome.Trim(),
            CreatedAt = agora
        });
    }

    public Result Atualizar(string nome, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure(GrupoMuscularErrors.NomeVazio);

        if (nome.Trim().Length > 50)
            return Result.Failure(GrupoMuscularErrors.NomeMuitoLongo);

        Nome = nome.Trim();
        UpdatedAt = agora;
        return Result.Success();
    }
}
