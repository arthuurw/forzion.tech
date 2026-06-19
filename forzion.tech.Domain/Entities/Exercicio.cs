using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class Exercicio
{
    public Guid Id { get; private set; }
    public Guid? TreinadorId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Guid GrupoMuscularId { get; private set; }
    public string? Descricao { get; private set; }
    public string? ComoExecutar { get; private set; }
    public string? VideoId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public bool IsGlobal => TreinadorId is null;

    private Exercicio() { }

    /// <param name="treinadorId">Null indica exercício da biblioteca global (gerenciado por admins).</param>
    public static Result<Exercicio> Criar(string nome, Guid grupoMuscularId, DateTime agora, Guid? treinadorId = null, string? descricao = null, string? comoExecutar = null, string? videoUrl = null)
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

        string? comoExecutarNormalizado = null;
        if (!string.IsNullOrWhiteSpace(comoExecutar))
        {
            var trimmed = comoExecutar.Trim();
            if (trimmed.Length > 2000)
                return Result.Failure<Exercicio>(ExercicioErrors.ComoExecutarMuitoLongo);
            comoExecutarNormalizado = trimmed;
        }

        string? videoId = null;
        if (!string.IsNullOrWhiteSpace(videoUrl))
        {
            var videoResult = YouTubeVideoId.Criar(videoUrl);
            if (videoResult.IsFailure)
                return Result.Failure<Exercicio>(videoResult.Error!);
            videoId = videoResult.Value.Value;
        }

        return Result.Success(new Exercicio
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Nome = nome.Trim(),
            GrupoMuscularId = grupoMuscularId,
            Descricao = descricao,
            ComoExecutar = comoExecutarNormalizado,
            VideoId = videoId,
            CreatedAt = agora
        });
    }

    public Result Atualizar(string? nome, Guid? grupoMuscularId, string? descricao, DateTime agora, string? comoExecutar = null, string? videoUrl = null)
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

        if (comoExecutar is not null)
        {
            var trimmed = comoExecutar.Trim();
            if (trimmed.Length > 2000)
                return Result.Failure(ExercicioErrors.ComoExecutarMuitoLongo);
            ComoExecutar = trimmed.Length == 0 ? null : trimmed;
        }

        if (videoUrl is not null)
        {
            if (videoUrl.Trim().Length == 0)
            {
                VideoId = null;
            }
            else
            {
                var videoResult = YouTubeVideoId.Criar(videoUrl);
                if (videoResult.IsFailure)
                    return Result.Failure(videoResult.Error!);
                VideoId = videoResult.Value.Value;
            }
        }

        UpdatedAt = agora;
        return Result.Success();
    }
}
