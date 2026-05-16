using System.Text.Json;
using FluentValidation;
using forzion.tech.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Middleware;

public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    const string defaultMessage = "Não encontrado";
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is ValidationException validationException)
            return await HandleValidationException(httpContext, validationException, cancellationToken).ConfigureAwait(false);

        var (statusCode, title, detail) = MapException(exception);

        if (statusCode >= 500)
            LogErroInesperado(_logger, exception, exception.Message);
        else
            LogErroDominio(_logger, exception.GetType().Name, exception.Message);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static async Task<bool> HandleValidationException(
        HttpContext httpContext,
        ValidationException exception,
        CancellationToken cancellationToken)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => JsonNamingPolicy.CamelCase.ConvertName(g.Key),
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Erro de validação",
            Detail = "Um ou mais erros de validação ocorreram.",
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception) =>
        exception switch
        {
            CredenciaisInvalidasException ex    => (StatusCodes.Status401Unauthorized,        "Não autorizado",  ex.Message),

            AlunoNaoEncontradoException ex              => (StatusCodes.Status404NotFound, defaultMessage, ex.Message),
            TreinadorNaoEncontradoException ex          => (StatusCodes.Status404NotFound, defaultMessage, ex.Message),
            TreinoNaoEncontradoException ex             => (StatusCodes.Status404NotFound, defaultMessage, ex.Message),
            VinculoNaoEncontradoException ex            => (StatusCodes.Status404NotFound, defaultMessage, ex.Message),
            ExercicioNaoEncontradoException ex          => (StatusCodes.Status404NotFound, defaultMessage, ex.Message),
            PacoteNaoEncontradoException ex             => (StatusCodes.Status404NotFound, defaultMessage, ex.Message),
            GrupoMuscularNaoEncontradoException ex      => (StatusCodes.Status404NotFound, defaultMessage, ex.Message),
            PlanoTreinadorNaoEncontradoException ex     => (StatusCodes.Status404NotFound, defaultMessage, ex.Message),

            AlunoInativoException ex            => (StatusCodes.Status403Forbidden, "Inativo",       ex.Message),
            AcessoNegadoException ex            => (StatusCodes.Status403Forbidden, "Acesso negado", ex.Message),

            EmailJaCadastradoException ex       => (StatusCodes.Status409Conflict, "Conflito", ex.Message),
            AlunoJaVinculadoException ex        => (StatusCodes.Status409Conflict, "Conflito", ex.Message),

            DomainException ex                  => (StatusCodes.Status422UnprocessableEntity, "Erro de domínio", ex.Message),

            _                                   => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado. Tente novamente mais tarde.")
        };

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro inesperado: {Message}")]
    private static partial void LogErroInesperado(ILogger logger, Exception exception, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erro de domínio [{Type}]: {Message}")]
    private static partial void LogErroDominio(ILogger logger, string type, string message);
}
