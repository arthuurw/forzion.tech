using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;
using forzion.tech.Application.UseCases.Alunos.AtualizarAluno;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Alunos;

public static class AlunoEndpoints
{
    public static void MapAlunoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/alunos").WithTags("Alunos");

        group.MapPost("/", async (
            [FromBody] CadastrarAlunoRequest request,
            CadastrarAlunoHandler handler,
            ITenantContext tenantContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var treinadorId = ObterSupabaseId(httpContext);
            if (treinadorId is null || tenantContext.TenantId is null)
                return Results.Unauthorized();

            var erros = ValidarCadastrarRequest(request);
            if (erros.Count > 0)
                return Results.ValidationProblem(erros);

            var command = new CadastrarAlunoCommand(
                tenantContext.TenantId.Value, treinadorId.Value, request.Nome, request.Email, request.Telefone);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Created($"/alunos/{response.AlunoId}", response);
        })
        .RequireAuthorization()
        .WithSummary("Cadastra um novo aluno")
        .Produces<AlunoResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("/", async (
            ListarAlunosHandler handler,
            ITenantContext tenantContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (tenantContext.TenantId is null)
                return Results.Unauthorized();

            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : tamanhoPagina > 100 ? 100 : tamanhoPagina;

            var query = new ListarAlunosQuery(tenantContext.TenantId.Value, p, tp);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Lista alunos do tenant com paginação")
        .Produces<ListarAlunosResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id}", async (
            Guid id,
            ObterAlunoHandler handler,
            ITenantContext tenantContext,
            CancellationToken cancellationToken) =>
        {
            if (tenantContext.TenantId is null)
                return Results.Unauthorized();

            var query = new ObterAlunoQuery(tenantContext.TenantId.Value, id);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Retorna os dados de um aluno")
        .Produces<AlunoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPatch("/{id}", async (
            Guid id,
            [FromBody] AtualizarAlunoRequest request,
            AtualizarAlunoHandler handler,
            ITenantContext tenantContext,
            CancellationToken cancellationToken) =>
        {
            if (tenantContext.TenantId is null)
                return Results.Unauthorized();

            var erros = ValidarAtualizarRequest(request);
            if (erros.Count > 0)
                return Results.ValidationProblem(erros);

            var command = new AtualizarAlunoCommand(
                tenantContext.TenantId.Value, id, request.Nome, request.Email, request.Telefone);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Atualiza os dados de um aluno")
        .Produces<AlunoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPatch("/{id}/status", async (
            Guid id,
            [FromBody] AlterarStatusAlunoRequest request,
            AlterarStatusAlunoHandler handler,
            ITenantContext tenantContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var adminId = ObterSupabaseId(httpContext);
            if (adminId is null || tenantContext.TenantId is null)
                return Results.Unauthorized();

            var command = new AlterarStatusAlunoCommand(
                tenantContext.TenantId.Value, adminId.Value, id, request.Status);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Altera o status de um aluno (somente Admin)")
        .Produces<AlunoResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static Guid? ObterSupabaseId(HttpContext context)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static Dictionary<string, string[]> ValidarCadastrarRequest(CadastrarAlunoRequest request)
    {
        var erros = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Nome))
            erros["nome"] = ["O nome é obrigatório."];
        else if (request.Nome.Length > 100)
            erros["nome"] = ["O nome deve ter no máximo 100 caracteres."];

        if (request.Email is not null)
        {
            if (request.Email.Length > 256)
                erros["email"] = ["O e-mail deve ter no máximo 256 caracteres."];
            else if (!request.Email.Contains('@'))
                erros["email"] = ["O e-mail informado é inválido."];
        }

        if (request.Telefone is not null && request.Telefone.Length > 20)
            erros["telefone"] = ["O telefone deve ter no máximo 20 caracteres."];

        return erros;
    }

    private static Dictionary<string, string[]> ValidarAtualizarRequest(AtualizarAlunoRequest request)
    {
        var erros = new Dictionary<string, string[]>();

        if (request.Nome is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Nome))
                erros["nome"] = ["O nome não pode ser vazio."];
            else if (request.Nome.Length > 100)
                erros["nome"] = ["O nome deve ter no máximo 100 caracteres."];
        }

        if (request.Email is not null && request.Email.Length > 0)
        {
            if (request.Email.Length > 256)
                erros["email"] = ["O e-mail deve ter no máximo 256 caracteres."];
            else if (!request.Email.Contains('@'))
                erros["email"] = ["O e-mail informado é inválido."];
        }

        if (request.Telefone is not null && request.Telefone.Length > 20)
            erros["telefone"] = ["O telefone deve ter no máximo 20 caracteres."];

        return erros;
    }
}

public record CadastrarAlunoRequest(string Nome, string? Email, string? Telefone);
public record AtualizarAlunoRequest(string? Nome, string? Email, string? Telefone);
public record AlterarStatusAlunoRequest(AlunoStatus Status);
