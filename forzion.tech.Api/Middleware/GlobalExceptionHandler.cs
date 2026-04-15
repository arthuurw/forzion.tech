using forzion.tech.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace forzion.tech.Api.Middleware;

public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

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

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception) =>
        exception switch
        {
            UsuarioJaRegistradoException ex  => (StatusCodes.Status409Conflict,            "Conflito",        ex.Message),
            UsuarioNaoEncontradoException ex  => (StatusCodes.Status404NotFound,            "Não encontrado",  ex.Message),
            UsuarioInativoException ex        => (StatusCodes.Status403Forbidden,           "Inativo",         ex.Message),
            AlunoNaoEncontradoException ex   => (StatusCodes.Status404NotFound,            "Não encontrado",  ex.Message),
            AlunoInativoException ex         => (StatusCodes.Status403Forbidden,           "Inativo",         ex.Message),
            AcessoNegadoException ex         => (StatusCodes.Status403Forbidden,           "Acesso negado",   ex.Message),
            DomainException ex               => (StatusCodes.Status422UnprocessableEntity, "Erro de domínio", ex.Message),
            _                                => (StatusCodes.Status500InternalServerError, "Erro interno",    "Ocorreu um erro inesperado. Tente novamente mais tarde.")
        };

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro inesperado: {Message}")]
    private static partial void LogErroInesperado(ILogger logger, Exception exception, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erro de domínio [{Type}]: {Message}")]
    private static partial void LogErroDominio(ILogger logger, string type, string message);
}
