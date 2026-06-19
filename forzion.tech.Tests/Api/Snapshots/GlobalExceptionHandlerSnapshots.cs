using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Api.Middleware;
using forzion.tech.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using static VerifyXunit.Verifier;

namespace forzion.tech.Tests.Api.Snapshots;

/// <summary>
/// Snapshot do mapa excecao -&gt; ProblemDetails do <see cref="GlobalExceptionHandler"/>.
/// Cada excecao mapeada vira uma linha (status + title + detail). Alterar o mapeamento
/// exige re-aprovar o snapshot conscientemente — esse e o sinal desejado.
/// </summary>
public class GlobalExceptionHandlerSnapshots
{
    private static readonly GlobalExceptionHandler Handler = new(NullLogger<GlobalExceptionHandler>.Instance);

    private static IEnumerable<Exception> ExcecoesMapeadas() =>
    [
        new CredenciaisInvalidasException(),
        new AlunoNaoEncontradoException(),
        new TreinadorNaoEncontradoException(),
        new TreinoNaoEncontradoException(),
        new VinculoNaoEncontradoException(),
        new ExercicioNaoEncontradoException(),
        new PacoteNaoEncontradoException(),
        new GrupoMuscularNaoEncontradoException(),
        new PlanoPlataformaNaoEncontradoException(),
        new AlunoInativoException(),
        new AcessoNegadoException(),
        new EmailJaCadastradoException(),
        new AlunoJaVinculadoException(),
        new DomainException("Regra de dominio violada."),
        new EstadoInconsistenteException("conta autenticada nao encontrada"),
        new InvalidOperationException("erro inesperado interno"),
    ];

    [Fact]
    public async Task MapeamentoDeExcecoes()
    {
        var mapeamentos = new List<object>();

        foreach (var excecao in ExcecoesMapeadas())
        {
            var (statusCode, body) = await ExecutarAsync(excecao);
            mapeamentos.Add(new
            {
                Exception = excecao.GetType().Name,
                StatusCode = statusCode,
                Title = body.GetProperty("title").GetString(),
                Detail = body.GetProperty("detail").GetString(),
                Code = body.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null,
            });
        }

        await Verify(mapeamentos);
    }

    [Fact]
    public async Task ValidationException_ProblemDetails()
    {
        var falhas = new[]
        {
            new ValidationFailure("Nome", "O nome e obrigatorio."),
            new ValidationFailure("Email", "O e-mail e invalido."),
        };

        var (statusCode, body) = await ExecutarAsync(new ValidationException(falhas));

        await Verify(new
        {
            StatusCode = statusCode,
            Title = body.GetProperty("title").GetString(),
            Detail = body.GetProperty("detail").GetString(),
            Errors = body.GetProperty("errors").ToString(),
        });
    }

    private static async Task<(int StatusCode, JsonElement Body)> ExecutarAsync(Exception excecao)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/contrato/teste";
        context.Response.Body = new MemoryStream();

        await Handler.TryHandleAsync(context, excecao, default);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, JsonDocument.Parse(json).RootElement.Clone());
    }
}
