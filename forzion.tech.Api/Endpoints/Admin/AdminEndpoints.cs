using forzion.tech.Api.Extensions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Admin.Alunos.ListarAlunosAdmin;
using forzion.tech.Application.UseCases.Admin.GruposMusculares;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ListarGruposMusculares;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;
using forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Application.UseCases.Alunos.ObterFichaAluno;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;
using forzion.tech.Application.UseCases.Exercicios;
using forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Api.Filters;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotesAluno;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Planos.AtualizarPlanoTreinador;
using forzion.tech.Application.UseCases.Planos.CriarPlanoTreinador;
using forzion.tech.Application.UseCases.Planos.ExcluirPlanoTreinador;
using forzion.tech.Application.UseCases.Planos.ListarPlanosTreinador;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;
using forzion.tech.Application.UseCases.Treinadores.AtribuirPlano;
using forzion.tech.Application.UseCases.Treinadores.ExcluirTreinador;
using forzion.tech.Application.UseCases.Treinadores.InativarTreinador;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadores;
using forzion.tech.Application.UseCases.Treinadores.ObterTreinador;
using forzion.tech.Application.UseCases.Treinadores.ReprovarTreinador;
using forzion.tech.Application.UseCases.Treinos;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;
using forzion.tech.Application.UseCases.Treinos.ListarTreinosDoTreinador;
using forzion.tech.Application.UseCases.Treinos.ObterTreino;
using forzion.tech.Application.UseCases.Vinculos.ListarVinculos;
using forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace forzion.tech.Api.Endpoints.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin").WithTags("Admin").RequireAuthorization("SystemAdmin")
            .RequireRateLimiting("write")
            .AddEndpointFilter<PaginacaoFilter>();

        group.MapGet("/treinadores", async (
            [FromServices] ListarTreinadoresHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var statusString = httpContext.Request.Query["status"].ToString();
            TreinadorStatus status = default;
            var statusParsed = !string.IsNullOrEmpty(statusString) && Enum.TryParse<TreinadorStatus>(statusString, ignoreCase: true, out status);
            _ = int.TryParse(httpContext.Request.Query["pagina"], out var pagina);
            _ = int.TryParse(httpContext.Request.Query["tamanhoPagina"], out var tamanhoPagina);
            var p = pagina < 1 ? 1 : pagina;
            var tp = tamanhoPagina < 1 ? 20 : Math.Clamp(tamanhoPagina, 1, 100);

            var result = await handler.HandleAsync(statusParsed ? status : null, p, tp, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista treinadores com filtro opcional por status")
        .Produces<ListarTreinadoresResponse>();

        group.MapGet("/treinadores/{id:guid}", async (
            Guid id,
            [FromServices] ObterTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Obtém os dados de um treinador")
        .Produces<TreinadorResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/treinadores/{id:guid}/aprovar", async (
            Guid id,
            [FromBody] AprovarTreinadorRequest request,
            [FromServices] AprovarTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AprovarTreinadorCommand(id, userContext.ContaId, request.Observacao), cancellationToken);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Aprova um treinador")
        .Produces<TreinadorResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/treinadores/{id:guid}/reprovar", async (
            Guid id,
            [FromBody] ReprovarTreinadorRequest request,
            [FromServices] ReprovarTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ReprovarTreinadorCommand(id, userContext.ContaId, request.Observacao), cancellationToken);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Reprova um treinador aguardando aprovação")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/treinadores/{id:guid}/inativar", async (
            Guid id,
            [FromBody] InativarTreinadorRequest request,
            [FromServices] InativarTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new InativarTreinadorCommand(id, userContext.ContaId, request.Observacao), cancellationToken);

            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Inativa um treinador e faz cascade nos vínculos")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/treinadores/{id:guid}", async (
            Guid id,
            [FromServices] ExcluirTreinadorHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new ExcluirTreinadorCommand(id, userContext.ContaId), cancellationToken);
            return Results.NoContent();
        })
        .WithSummary("Exclui permanentemente um treinador inativo e todas as suas dependências. LogAprovacao é preservado.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPatch("/treinadores/{id:guid}/plano", async (
            Guid id,
            [FromBody] AtribuirPlanoRequest request,
            [FromServices] AtribuirPlanoHandler handler,
            [FromServices] IUserContext userContext,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AtribuirPlanoCommand(id, request.PlanoId, userContext.ContaId), cancellationToken);

            return Results.Ok(result);
        })
        .WithSummary("Atribui um PlanoTreinador a um treinador")
        .Produces<TreinadorResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/planos", async (
            [FromServices] ListarPlanosTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista todos os planos de treinador")
        .Produces<IReadOnlyList<PlanoTreinadorResponse>>();

        group.MapPost("/planos", async (
            [FromBody] CriarPlanoTreinadorRequest request,
            [FromServices] CriarPlanoTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CriarPlanoTreinadorCommand(request.Nome, request.Tier, request.MaxAlunos, request.Preco, request.Descricao), cancellationToken);

            return Results.Created($"/admin/planos/{result.PlanoId}", result);
        })
        .WithSummary("Cria um novo plano de treinador")
        .Produces<PlanoTreinadorResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/planos/{id:guid}", async (
            Guid id,
            [FromBody] AtualizarPlanoTreinadorRequest request,
            [FromServices] AtualizarPlanoTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AtualizarPlanoTreinadorCommand(id, request.Nome, request.Tier, request.MaxAlunos, request.Preco, request.Descricao), cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Atualiza nome, maxAlunos e/ou preço de um plano")
        .Produces<PlanoTreinadorResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/planos/{id:guid}", async (
            Guid id,
            [FromServices] ExcluirPlanoTreinadorHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new ExcluirPlanoTreinadorCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .WithSummary("Inativa um plano de treinador")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Grupos Musculares
        group.MapGet("/grupos-musculares", async (
            [FromServices] ListarGruposMuscularesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista todos os grupos musculares")
        .Produces<IReadOnlyList<GrupoMuscularResponse>>();

        group.MapPost("/grupos-musculares", async (
            [FromBody] CriarGrupoMuscularRequest request,
            [FromServices] CriarGrupoMuscularHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new CriarGrupoMuscularCommand(request.Nome), cancellationToken);
            return Results.Created($"/admin/grupos-musculares/{result.Id}", result);
        })
        .WithSummary("Cria um novo grupo muscular")
        .Produces<GrupoMuscularResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/grupos-musculares/{id:guid}", async (
            Guid id,
            [FromBody] AtualizarGrupoMuscularRequest request,
            [FromServices] AtualizarGrupoMuscularHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new AtualizarGrupoMuscularCommand(id, request.Nome), cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Atualiza um grupo muscular")
        .Produces<GrupoMuscularResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/grupos-musculares/{id:guid}", async (
            Guid id,
            [FromServices] ExcluirGrupoMuscularHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new ExcluirGrupoMuscularCommand(id), cancellationToken);
            return Results.NoContent();
        })
        .WithSummary("Exclui um grupo muscular")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/exercicios", async (
            [FromServices] ListarExerciciosHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var pagination = httpContext.ObterPaginacaoDoQuery();
            var q = httpContext.Request.Query;
            var hasGrupo = Guid.TryParse(q["grupoMuscularId"], out var grupoId);
            var nome = q["nome"].ToString();
            var ordenarPor = q["ordenarPor"].ToString();
            var result = await handler.HandleAsync(
                new ListarExerciciosQuery(null, pagination.Pagina, pagination.TamanhoPagina,
                    string.IsNullOrEmpty(nome) ? null : nome,
                    hasGrupo ? grupoId : null,
                    string.IsNullOrEmpty(ordenarPor) ? "nome" : ordenarPor),
                cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista exercícios da biblioteca global")
        .Produces<ListarExerciciosResponse>();

        group.MapPost("/exercicios", async (
            [FromBody] CriarExercicioGlobalRequest request,
            [FromServices] CriarExercicioHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CriarExercicioCommand(null, request.Nome, request.GrupoMuscularId, request.Descricao),
                cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Created($"/admin/exercicios/{result.Value.ExercicioId}", result.Value);
        })
        .WithSummary("Cria exercício na biblioteca global (TreinadorId = null)")
        .Produces<ExercicioResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/exercicios/{id:guid}", async (
            Guid id,
            [FromBody] AtualizarExercicioGlobalRequest request,
            [FromServices] AtualizarExercicioHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AtualizarExercicioCommand(id, null, request.Nome, request.GrupoMuscularId, request.Descricao),
                cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.Ok(result.Value);
        })
        .WithSummary("Atualiza exercício global")
        .Produces<ExercicioResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/exercicios/{id:guid}", async (
            Guid id,
            [FromServices] ExcluirExercicioHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ExcluirExercicioCommand(id, null), cancellationToken);
            if (result.IsFailure) return result.ToProblemResult();
            return Results.NoContent();
        })
        .WithSummary("Exclui exercício global")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // --- Alunos (visibilidade admin) ---

        group.MapGet("/alunos", async (
            [FromServices] ListarAlunosAdminHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var pagination = httpContext.ObterPaginacaoDoQuery();
            var q = httpContext.Request.Query;
            _ = Enum.TryParse<AlunoStatus>(q["status"], out var status);
            var hasStatus = q.ContainsKey("status");
            var nome = q["nome"].ToString();

            var query = new ListarAlunosAdminQuery(
                pagination.Pagina,
                pagination.TamanhoPagina,
                string.IsNullOrWhiteSpace(nome) ? null : nome,
                hasStatus ? status : null);

            var result = await handler.HandleAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista todos os alunos do sistema com filtros opcionais (nome, status)")
        .Produces<ListarAlunosResponse>();

        group.MapGet("/alunos/{id:guid}", async (
            Guid id,
            [FromServices] ObterAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ObterAlunoQuery(id), cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Obtém os dados de um aluno")
        .Produces<AlunoResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/alunos/{id:guid}/vinculo", async (
            Guid id,
            [FromServices] ObterVinculoAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Obtém o vínculo atual (ativo e pendente) de um aluno")
        .Produces<ObterVinculoAlunoResponse>();

        group.MapGet("/alunos/{id:guid}/fichas", async (
            Guid id,
            [FromServices] ListarFichasAlunoHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var pagination = httpContext.ObterPaginacaoDoQuery();
            var result = await handler.HandleAsync(id, pagination.Pagina, pagination.TamanhoPagina, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista fichas de treino de um aluno")
        .Produces<ListarFichasAlunoResponse>();

        group.MapGet("/fichas/{treinoAlunoId:guid}", async (
            Guid treinoAlunoId,
            [FromServices] ObterFichaAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(treinoAlunoId, Guid.Empty, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Obtém o detalhe de uma ficha vinculada a um aluno")
        .Produces<FichaAlunoDetalheResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/alunos/{id:guid}/execucoes", async (
            Guid id,
            [FromServices] ListarExecucoesAlunoHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var pagination = httpContext.ObterPaginacaoDoQuery();
            var result = await handler.HandleAsync(id, pagination.Pagina, pagination.TamanhoPagina, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista execuções de treino de um aluno")
        .Produces<ListarExecucoesAlunoResponse>();

        group.MapGet("/alunos/{id:guid}/progressao", async (
            Guid id,
            [FromServices] ObterProgressaoAlunoHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var hoje = DateTime.UtcNow.Date;
            var de = DateTime.TryParse(httpContext.Request.Query["de"], System.Globalization.CultureInfo.InvariantCulture, out var deParsed)
                ? deParsed.Date
                : hoje.AddDays(-90);
            var ate = DateTime.TryParse(httpContext.Request.Query["ate"], System.Globalization.CultureInfo.InvariantCulture, out var ateParsed)
                ? ateParsed.Date
                : hoje;

            if (de > ate)
                return Results.BadRequest("O parâmetro 'de' deve ser anterior a 'ate'.");

            var result = await handler.HandleAsync(new ObterProgressaoAlunoQuery(id, de, ate), cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Retorna a progressão de carga de um aluno no período")
        .Produces<ProgressaoAlunoResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // --- Sub-recursos de treinadores (visibilidade admin) ---

        group.MapGet("/treinadores/{id:guid}/alunos", async (
            Guid id,
            [FromServices] ListarAlunosHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var pagination = httpContext.ObterPaginacaoDoQuery();
            var result = await handler.HandleAsync(
                new ListarAlunosQuery(id, pagination.Pagina, pagination.TamanhoPagina), cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista alunos ativos de um treinador")
        .Produces<ListarAlunosResponse>();

        group.MapGet("/treinadores/{id:guid}/vinculos", async (
            Guid id,
            [FromServices] ListarVinculosHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var pagination = httpContext.ObterPaginacaoDoQuery();
            var statusString = httpContext.Request.Query["status"].ToString();
            VinculoStatus status = default;
            var statusParsed = !string.IsNullOrEmpty(statusString) && Enum.TryParse<VinculoStatus>(statusString, ignoreCase: true, out status);

            var result = await handler.HandleAsync(
                id, statusParsed ? status : null, pagination.Pagina, pagination.TamanhoPagina, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista vínculos de um treinador com paginação e filtro opcional por status")
        .Produces<ListarVinculosResponse>();

        group.MapGet("/treinadores/{id:guid}/treinos", async (
            Guid id,
            [FromServices] ListarTreinosDoTreinadorHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var pagination = httpContext.ObterPaginacaoDoQuery();
            var nome = httpContext.Request.Query["nome"].FirstOrDefault();
            var objetivo = httpContext.Request.Query["objetivo"].FirstOrDefault();
            var ordenarPorRaw = httpContext.Request.Query["ordenarPor"].FirstOrDefault();

            var ordenacaoPermitida = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "nome", "objetivo", "nomeAluno", "createdAt" };
            var ordenarPor = !string.IsNullOrWhiteSpace(ordenarPorRaw) && ordenacaoPermitida.Contains(ordenarPorRaw)
                ? ordenarPorRaw
                : null;

            var result = await handler.HandleAsync(
                id, pagination.Pagina, pagination.TamanhoPagina, nome, objetivo, ordenarPor, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista treinos de um treinador")
        .Produces<ListarTreinosResponse>();

        group.MapGet("/treinos/{id:guid}", async (
            Guid id,
            [FromServices] ObterTreinoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ObterTreinoQuery(id), cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Obtém o detalhe de um treino")
        .Produces<TreinoResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/treinadores/{id:guid}/pacotes", async (
            Guid id,
            [FromServices] ListarPacotesAlunoHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, cancellationToken);
            return Results.Ok(result);
        })
        .WithSummary("Lista pacotes de um treinador")
        .Produces<IReadOnlyList<PacoteAlunoResponse>>();

        return endpoints;
    }
}

public record AprovarTreinadorRequest(string? Observacao = null);
public record ReprovarTreinadorRequest(string? Observacao = null);
public record InativarTreinadorRequest(string? Observacao = null);
public record AtribuirPlanoRequest(Guid PlanoId);
public record CriarPlanoTreinadorRequest(string Nome, forzion.tech.Domain.Enums.TierPlano Tier, int MaxAlunos, decimal Preco, string? Descricao = null);
public record AtualizarPlanoTreinadorRequest(string? Nome, forzion.tech.Domain.Enums.TierPlano? Tier, int? MaxAlunos, decimal? Preco, string? Descricao = null);
public record CriarGrupoMuscularRequest(string Nome);
public record AtualizarGrupoMuscularRequest(string Nome);
public record CriarExercicioGlobalRequest(string Nome, Guid GrupoMuscularId, string? Descricao);
public record AtualizarExercicioGlobalRequest(string? Nome, Guid? GrupoMuscularId, string? Descricao);
