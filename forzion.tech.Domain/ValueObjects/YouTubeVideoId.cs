using System.Text.RegularExpressions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.ValueObjects;

public sealed record YouTubeVideoId
{
    private static readonly Regex IdPuro = new(
        @"^[A-Za-z0-9_-]{11}$",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(1));

    // Lookahead garante boundary (id de 11 chars exatos, não trunca token maior) ⇒ incompatível
    // com NonBacktracking; padrão é linear (sem quantificador aninhado), sem risco de ReDoS.
    private static readonly Regex DeUrl = new(
        @"(?:youtu\.be/|/shorts/|/embed/|[?&]v=)([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    public string Value { get; }

    private YouTubeVideoId(string value) => Value = value;

    public static Result<YouTubeVideoId> Criar(string urlOuId)
    {
        if (string.IsNullOrWhiteSpace(urlOuId))
            return Result.Failure<YouTubeVideoId>(ExercicioErrors.VideoUrlInvalida);

        var entrada = urlOuId.Trim();

        if (IdPuro.IsMatch(entrada))
            return Result.Success(new YouTubeVideoId(entrada));

        var match = DeUrl.Match(entrada);
        if (match.Success)
            return Result.Success(new YouTubeVideoId(match.Groups[1].Value));

        return Result.Failure<YouTubeVideoId>(ExercicioErrors.VideoUrlInvalida);
    }

    public static YouTubeVideoId FromDatabase(string value) => new(value);

    public override string ToString() => Value;
}
