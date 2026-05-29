using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class Exercicio
{
    public Guid Id { get; private set; }
    public Guid? TreinadorId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Guid GrupoMuscularId { get; private set; }
    public string? Descricao { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public bool IsGlobal => TreinadorId is null;

    private Exercicio() { }

    /// <param name="treinadorId">Null indica exercício da biblioteca global (gerenciado por admins).</param>
    public static Result<Exercicio> Criar(string nome, Guid grupoMuscularId, DateTime agora, Guid? treinadorId = null, string? descricao = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure<Exercicio>(ExercicioErrors.NomeObrigatorio);
        if (nome.Trim().Length > 100)
            return Result.Failure<Exercicio>(ExercicioErrors.NomeMuitoLongo);
        if (grupoMuscularId == Guid.Empty)
            return Result.Failure<Exercicio>(ExercicioErrors.GrupoMuscularObrigatorio);
        if (treinadorId.HasValue && treinadorId.Value == Guid.Empty)
            return Result.Failure<Exercicio>(ExercicioErrors.TreinadorIdInvalido);
        if (descricao is not null && descricao.Length > 500)
            return Result.Failure<Exercicio>(ExercicioErrors.DescricaoMuitoLonga);

        return Result.Success(new Exercicio
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Nome = nome.Trim(),
            GrupoMuscularId = grupoMuscularId,
            Descricao = descricao,
            CreatedAt = agora
        });
    }

    public Result Atualizar(string? nome, Guid? grupoMuscularId, string? descricao, DateTime agora)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return Result.Failure(ExercicioErrors.NomeVazio);
            if (nome.Trim().Length > 100)
                return Result.Failure(ExercicioErrors.NomeMuitoLongo);
            Nome = nome.Trim();
        }

        if (grupoMuscularId is not null)
        {
            if (grupoMuscularId.Value == Guid.Empty)
                return Result.Failure(ExercicioErrors.GrupoMuscularObrigatorio);
            GrupoMuscularId = grupoMuscularId.Value;
        }

        if (descricao is not null)
        {
            if (descricao.Length > 500)
                return Result.Failure(ExercicioErrors.DescricaoMuitoLonga);
            Descricao = descricao.Length == 0 ? null : descricao;
        }

        UpdatedAt = agora;
        return Result.Success();
    }
}
