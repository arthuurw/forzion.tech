using forzion.tech.Domain.Shared;

namespace forzion.tech.Api.Extensions;

public static class ResultExtensions
{
    // Mapeia o tipo do erro de negócio p/ status HTTP. Business (default) → 422,
    // preservando o comportamento histórico; tipos novos refinam o status.
    public static IResult ToProblemResult(this Result result)
    {
        var error = result.Error!;
        var (statusCode, title) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Não encontrado."),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflito."),
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Requisição inválida."),
            _ => (StatusCodes.Status422UnprocessableEntity, "Não foi possível processar."),
        };

        return Microsoft.AspNetCore.Http.Results.Problem(
            detail: error.Message,
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
