using forzion.tech.Api.Extensions;
using forzion.tech.Domain.Enums;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Application.UseCases.Auth.RedefinirSenha;
using forzion.tech.Application.UseCases.Auth.VerificarEmail;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotes;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Planos.ListarPlanosPlataforma;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadoresPublicos;
using forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", async (
            LoginRequest request,
            [FromServices] LoginHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new LoginCommand(request.Email, request.Senha), cancellationToken);

            return Results.Ok(result);
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithName("Login")
        .Produces<LoginResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/register/treinador", async (
            RegistrarTreinadorRequest request,
            [FromServices] RegistrarTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new RegistrarTreinadorCommand(request.Email, request.Senha, request.Nome, request.PlanoPlataformaId, request.ModoPagamentoAluno, request.Telefone), cancellationToken);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Created($"/treinador/perfil", result.Value);
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithSummary("Cadastra um novo treinador (aguarda aprovação)")
        .Produces<TreinadorResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/register/aluno", async (
            RegistrarAlunoRequest request,
            [FromServices] RegistrarAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new RegistrarAlunoCommand(request.Email, request.Senha, request.Nome, request.TreinadorId, request.PacoteId, request.Telefone),
                cancellationToken);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Created($"/aluno/perfil", result.Value);
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithSummary("Cadastra um novo aluno e cria vínculo pendente com o treinador")
        .Produces<AlunoResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest request,
            [FromServices] EsqueceuSenhaHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new EsqueceuSenhaCommand(request.Email), cancellationToken);
            return Results.Ok();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithSummary("Envia e-mail de redefinição de senha (sempre retorna 200)")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/reset-password", async (
            ResetPasswordRequest request,
            [FromServices] RedefinirSenhaHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new RedefinirSenhaCommand(request.Token, request.NovaSenha), cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithSummary("Redefine a senha usando token enviado por e-mail")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/verify-email", async (
            VerifyEmailRequest request,
            [FromServices] VerificarEmailHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new VerificarEmailCommand(request.Token), cancellationToken);
            return Results.Ok();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithSummary("Verifica o e-mail usando token enviado por e-mail")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/resend-verification", async (
            ResendVerificationRequest request,
            [FromServices] ReenviarVerificacaoHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new ReenviarVerificacaoCommand(request.Email), cancellationToken);
            return Results.Ok();
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithSummary("Reenvia o e-mail de verificação (sempre retorna 200)")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/planos", async (
            [FromServices] ListarPlanosPlataformaHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithSummary("Lista todos os planos de treinador disponíveis")
        .Produces<IReadOnlyList<PlanoPlataformaResponse>>();

        group.MapGet("/treinadores/{id:guid}/pacotes", async (
            Guid id,
            [FromServices] ListarPacotesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, cancellationToken);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithSummary("Lista pacotes de um treinador específico (para escolha do aluno no cadastro)")
        .Produces<IReadOnlyList<PacoteResponse>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/treinadores", async (
            [FromServices] ListarTreinadoresPublicosHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .WithSummary("Lista treinadores ativos para o fluxo público de cadastro")
        .Produces<IReadOnlyList<TreinadorResponse>>();

        return endpoints;
    }
}

public record LoginRequest(string Email, string Senha);
public record RegistrarTreinadorRequest(string Email, string Senha, string Nome, Guid PlanoPlataformaId, ModoPagamentoAluno ModoPagamentoAluno, string? Telefone = null);
public record RegistrarAlunoRequest(string Email, string Senha, string Nome, Guid TreinadorId, Guid PacoteId, string? Telefone = null);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NovaSenha);
public record VerifyEmailRequest(string Token);
public record ResendVerificationRequest(string Email);
