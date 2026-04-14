using System.ComponentModel.DataAnnotations;
using forzion.tech.Application.UseCases.Usuarios.AtualizarUsuario;
using forzion.tech.Application.UseCases.Usuarios.ObterUsuarioAtual;
using forzion.tech.Application.UseCases.Usuarios.RegistrarUsuario;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Usuarios;

public static class UsuarioEndpoints
{
    public static void MapUsuarioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/usuarios").WithTags("Usuarios");

        group.MapGet("/me", async (
            ObterUsuarioAtualHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var usuarioId = ObterSupabaseId(httpContext);
            if (usuarioId is null)
                return Results.Unauthorized();

            var query = new ObterUsuarioAtualQuery(usuarioId.Value);
            var response = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Retorna os dados do usuário autenticado")
        .Produces<ObterUsuarioAtualResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPatch("/me", async (
            [FromBody] AtualizarUsuarioRequest request,
            AtualizarUsuarioHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var usuarioId = ObterSupabaseId(httpContext);
            if (usuarioId is null)
                return Results.Unauthorized();

            var erros = ValidarAtualizarRequest(request);
            if (erros.Count > 0)
                return Results.ValidationProblem(erros);

            var command = new AtualizarUsuarioCommand(usuarioId.Value, request.Nome, request.FotoUrl, request.Bio, request.Status);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithSummary("Atualiza o perfil do usuário autenticado")
        .Produces<ObterUsuarioAtualResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/registrar", async (
            [FromBody] RegistrarUsuarioRequest request,
            RegistrarUsuarioHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var supabaseId = ObterSupabaseId(httpContext);
            if (supabaseId is null)
                return Results.Unauthorized();

            var erros = ValidarRequest(request);
            if (erros.Count > 0)
                return Results.ValidationProblem(erros);

            var command = new RegistrarUsuarioCommand(supabaseId.Value, request.Nome, request.Email, request.TenantNome);
            var response = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            return Results.Created($"/usuarios/{response.UsuarioId}", response);
        })
        .RequireAuthorization()
        .WithSummary("Registra o perfil do usuário após autenticação no Supabase")
        .Produces<RegistrarUsuarioResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    private static Guid? ObterSupabaseId(HttpContext context)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static Dictionary<string, string[]> ValidarAtualizarRequest(AtualizarUsuarioRequest request)
    {
        var erros = new Dictionary<string, string[]>();

        if (request.Nome is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Nome))
                erros["nome"] = ["O nome não pode ser vazio."];
            else if (request.Nome.Length > 100)
                erros["nome"] = ["O nome deve ter no máximo 100 caracteres."];
        }

        if (request.FotoUrl is not null && request.FotoUrl.Length > 500)
            erros["fotoUrl"] = ["A URL da foto deve ter no máximo 500 caracteres."];

        if (request.Bio is not null && request.Bio.Length > 500)
            erros["bio"] = ["A bio deve ter no máximo 500 caracteres."];

        return erros;
    }

    private static Dictionary<string, string[]> ValidarRequest(RegistrarUsuarioRequest request)
    {
        var erros = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Nome))
            erros["nome"] = ["O nome é obrigatório."];
        else if (request.Nome.Length > 100)
            erros["nome"] = ["O nome deve ter no máximo 100 caracteres."];

        if (string.IsNullOrWhiteSpace(request.Email))
            erros["email"] = ["O e-mail é obrigatório."];
        else if (!new EmailAddressAttribute().IsValid(request.Email))
            erros["email"] = ["O e-mail informado é inválido."];
        else if (request.Email.Length > 256)
            erros["email"] = ["O e-mail deve ter no máximo 256 caracteres."];

        if (string.IsNullOrWhiteSpace(request.TenantNome))
            erros["tenantNome"] = ["O nome do tenant é obrigatório."];
        else if (request.TenantNome.Length > 100)
            erros["tenantNome"] = ["O nome do tenant deve ter no máximo 100 caracteres."];

        return erros;
    }
}

public record RegistrarUsuarioRequest(string Nome, string Email, string TenantNome);
public record AtualizarUsuarioRequest(string? Nome, string? FotoUrl, string? Bio, UsuarioStatus? Status);
