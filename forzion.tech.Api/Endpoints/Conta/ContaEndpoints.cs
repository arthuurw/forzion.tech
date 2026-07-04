using forzion.tech.Api.Extensions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Conta.AlterarSenha;
using forzion.tech.Application.UseCases.Conta.AtualizarPerfil;
using forzion.tech.Application.UseCases.Conta.Lgpd;
using forzion.tech.Application.UseCases.Conta.Logout;
using forzion.tech.Application.UseCases.Conta.ObterPerfil;
using forzion.tech.Application.UseCases.Conta.PreferenciasNotificacao;
using forzion.tech.Application.UseCases.Conta.TrocarEmail;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Conta;

public static class ContaEndpoints
{
    public static IEndpointRouteBuilder MapContaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/conta").WithTags("Conta").RequireAuthorization().RequireRateLimiting("write");

        group.MapGet("/perfil", async (
            [FromServices] ObterPerfilHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Retorna o perfil do usuário autenticado")
        .Produces<PerfilResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPatch("/perfil", async (
            [FromBody] AtualizarPerfilRequest request,
            [FromServices] AtualizarPerfilHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new AtualizarPerfilCommand(request.Nome), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Atualiza o nome do usuário autenticado")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPatch("/preferencias-notificacao", async (
            [FromBody] PreferenciasNotificacaoRequest request,
            [FromServices] AtualizarPreferenciaNotificacaoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new AtualizarPreferenciaNotificacaoCommand(request.EmailEngajamentoOptOut), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Atualiza a preferência de e-mails de engajamento do usuário autenticado")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/senha", async (
            [FromBody] AlterarSenhaRequest request,
            [FromServices] AlterarSenhaHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new AlterarSenhaCommand(request.SenhaAtual, request.NovaSenha), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .AddEndpointFilter<RequerStepUpFilter>()
        .WithSummary("Altera a senha do usuário autenticado")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        group.MapPost("/email/trocar", async (
            [FromBody] TrocarEmailRequest request,
            [FromServices] SolicitarTrocaEmailHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new SolicitarTrocaEmailCommand(userContext.ContaId, request.NovoEmail), cancellationToken).ConfigureAwait(false);
            return Results.Accepted();
        })
        .AddEndpointFilter<RequerStepUpFilter>()
        .WithSummary("Inicia a troca de e-mail enviando um código ao novo endereço")
        .Produces(StatusCodes.Status202Accepted)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPost("/email/confirmar", async (
            [FromBody] ConfirmarEmailRequest request,
            [FromServices] ConfirmarTrocaEmailHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ConfirmarTrocaEmailCommand(userContext.ContaId, userContext.Jti, userContext.TokenExpiraEm, request.Codigo),
                cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Confirma a troca de e-mail com o código recebido no novo endereço")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPost("/logout", async (
            [FromServices] LogoutHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Revoga o token atual e faz logout")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        var lgpdGroup = endpoints.MapGroup("/conta/lgpd")
            .WithTags("Conta")
            .RequireAuthorization()
            .RequireRateLimiting("write");

        lgpdGroup.MapGet("/exportar", async (
            string? formato,
            [FromServices] ExportarDadosPessoaisHandler handler,
            [FromServices] IDadosPessoaisExcelRenderer excelRenderer,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler
                .HandleAsync(new ExportarDadosPessoaisCommand(userContext.ContaId, userContext.ContaId), cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();

            if (string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = excelRenderer.Render(result.Value);
                return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "meus-dados.xlsx");
            }

            return Results.Ok(result.Value);
        })
        .WithSummary("Exporta os dados pessoais do titular autenticado (portabilidade LGPD)")
        .Produces<DadosPessoaisExport>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        lgpdGroup.MapDelete("/", async (
            [FromBody] AnonimizarContaSelfRequest request,
            [FromServices] AnonimizarContaHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler
                .HandleAsync(new AnonimizarContaCommand(
                    userContext.ContaId,
                    userContext.ContaId,
                    request.Senha), cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Anonimiza permanentemente a conta do titular (exclusão LGPD). Requer confirmação de senha.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }
}

public record AtualizarPerfilRequest(string Nome);
public record PreferenciasNotificacaoRequest(bool EmailEngajamentoOptOut);
public record AlterarSenhaRequest(string SenhaAtual, string NovaSenha);
public record AnonimizarContaSelfRequest(string Senha);
public record TrocarEmailRequest(string NovoEmail);
public record ConfirmarEmailRequest(string Codigo);
