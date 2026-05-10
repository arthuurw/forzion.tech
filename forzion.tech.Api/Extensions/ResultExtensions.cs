using forzion.tech.Application.Results;

namespace forzion.tech.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ToProblemResult(this Result result) =>
        Microsoft.AspNetCore.Http.Results.Problem(
            detail: result.Error!.Message,
            statusCode: StatusCodes.Status422UnprocessableEntity);
}
