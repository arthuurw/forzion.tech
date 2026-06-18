using forzion.tech.Domain.Shared;

namespace forzion.tech.Api.Extensions;

public static class ResultExtensions
{
    // Mapeia o tipo do erro de negócio p/ status HTTP. Business (default) → 422,
    // preservando o comportamento histórico; tipos novos refinam o status.
    public static IResult ToProblemResult(this Result result)
    {
        var error = result.Error!;
        var statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

        return Microsoft.AspNetCore.Http.Results.Problem(
            detail: error.Message,
            statusCode: statusCode,
            title: ProblemDetailsTitulos.PtBr.GetValueOrDefault(statusCode),
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
