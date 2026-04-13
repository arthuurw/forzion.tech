using System.ComponentModel.DataAnnotations;
using forzion.tech.Application.UseCases.Usuarios.RegistrarUsuario;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Usuarios;

public static class UsuarioEndpoints
{
    public static void MapUsuarioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/usuarios").WithTags("Usuarios");

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
