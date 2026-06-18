using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Auth;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Application.UseCases.Admin.Alunos.ListarAlunosAdmin;
using forzion.tech.Application.UseCases.Admin.GruposMusculares;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ListarGruposMusculares;
using forzion.tech.Application.UseCases.Admin.NotasFiscais;
using forzion.tech.Application.Outbox;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;
using forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Application.UseCases.Alunos.ObterFichaAluno;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotes;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Planos.AtualizarPlanoPlataforma;
using forzion.tech.Application.UseCases.Planos.CriarPlanoPlataforma;
using forzion.tech.Application.UseCases.Planos.ExcluirPlanoPlataforma;
using forzion.tech.Application.UseCases.Planos.ListarPlanosPlataforma;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;
using forzion.tech.Application.UseCases.Treinadores.AtribuirPlano;
using forzion.tech.Application.UseCases.Treinadores.ExcluirTreinador;
using forzion.tech.Application.UseCases.Treinadores.InativarTreinador;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadores;
using forzion.tech.Application.UseCases.Exercicios;
using forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Application.UseCases.Treinadores.ObterTreinador;
using forzion.tech.Application.UseCases.Treinadores.ReprovarTreinador;
using forzion.tech.Application.UseCases.Treinos;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;
using forzion.tech.Application.UseCases.Treinos.ListarTreinosDoTreinador;
using forzion.tech.Application.UseCases.Treinos.ObterTreino;
using forzion.tech.Application.UseCases.Conta.Lgpd;
using forzion.tech.Application.UseCases.Vinculos.ListarVinculos;
using forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FluentValidation;

namespace forzion.tech.Tests.Api.Endpoints;

public class AdminEndpointsTests : IClassFixture<AdminEndpointsTests.AdminWebFactory>
{
    private readonly AdminWebFactory _factory;

    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid TreinoAlunoId = Guid.NewGuid();
    private static readonly Guid TreinoId = Guid.NewGuid();

    private static readonly TreinadorResponse RespostaTreinador = new(
        TreinadorId, Guid.NewGuid(), "Carlos", TreinadorStatus.AguardandoAprovacao, null, DateTime.UtcNow);

    private static readonly PlanoPlataformaResponse RespostaPlano = new(
        Guid.NewGuid(), "Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99m, true, DateTime.UtcNow, null);

    private static readonly GrupoMuscularResponse RespostaGrupo = new(
        Guid.NewGuid(), "Peitoral", DateTime.UtcNow, null);

    private static readonly AlunoResponse RespostaAluno = new(
        AlunoId, "João Silva", "joao@test.com", null, AlunoStatus.Ativo, Guid.NewGuid(),
        DateTime.UtcNow, null);

    private static readonly ListarAlunosResponse RespostaListaAlunos =
        new([RespostaAluno], 1, 1, 20);

    private static readonly ObterVinculoAlunoResponse RespostaVinculo =
        new(null, null);

    private static readonly ListarFichasAlunoResponse RespostaFichas =
        new([], 0, 1, 20);

    private static readonly FichaAlunoDetalheResponse RespostaFichaDetalhe = new(
        TreinoAlunoId, TreinoId, "Treino A", ObjetivoTreino.Hipertrofia, "Ativo", []);

    private static readonly ListarExecucoesAlunoResponse RespostaExecucoes =
        new([], 0, 1, 20);

    private static readonly ProgressaoAlunoResponse RespostaProgressao =
        new([]);

    private static readonly ListarVinculosResponse RespostaVinculos =
        new([], 0, 1, 20);

    private static readonly ListarTreinosResponse RespostaTreinos =
        new([], 0, 1, 20);

    private static readonly TreinoResponse RespostaTreino = new(
        TreinoId, "Treino A", ObjetivoTreino.Hipertrofia, DificuldadeTreino.Iniciante,
        null, null, TreinadorId, [], DateTime.UtcNow, null);

    private static readonly PacoteResponse RespostaPacote = new(
        Guid.NewGuid(), TreinadorId, "Pacote Básico", null, 99m, true, DateTime.UtcNow, null);

    private static readonly ExercicioResponse RespostaExercicio = new(
        Guid.NewGuid(), "Supino", Guid.NewGuid(), "Peito", null, null, null, null, true, DateTime.UtcNow, null);

    private static readonly ListarExerciciosResponse RespostaExercicios =
        new([RespostaExercicio], 1, 1, 20);

    public AdminEndpointsTests(AdminWebFactory factory)
    {
        _factory = factory;
    }

    private const string StepUpTokenValido = "step-up-ok";

    private HttpClient CriarClienteAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "admin");
        return client;
    }

    private HttpClient CriarClienteAdminComStepUp()
    {
        var client = CriarClienteAdmin();
        client.DefaultRequestHeaders.Add("X-Step-Up-Token", StepUpTokenValido);
        return client;
    }

    private HttpClient CriarClienteNaoAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "treinador");
        return client;
    }

    // --- GET /admin/treinadores ---

    [Fact]
    public async Task Get_Treinadores_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/admin/treinadores");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Treinadores_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync("/admin/treinadores");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Treinadores_Admin_Retorna200()
    {
        var lista = new ListarTreinadoresResponse(new[] { RespostaTreinador }, 1, 1, 20);
        _factory.ListarTreinadoresHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<TreinadorStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lista);

        var response = await CriarClienteAdmin().GetAsync("/admin/treinadores");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Get_Treinadores_PaginaInvalida_Retorna400()
    {
        var response = await CriarClienteAdmin().GetAsync("/admin/treinadores?pagina=0");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Treinadores_TamanhoPaginaInvalido_Retorna400()
    {
        var response = await CriarClienteAdmin().GetAsync("/admin/treinadores?tamanhoPagina=200");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- POST /admin/treinadores/{id}/aprovar ---

    [Fact]
    public async Task Post_AprovarTreinador_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/aprovar", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AprovarTreinador_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/aprovar", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_AprovarTreinador_Admin_Retorna200()
    {
        var aprovado = RespostaTreinador with { Status = TreinadorStatus.Ativo };
        _factory.AprovarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AprovarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(aprovado));

        var response = await CriarClienteAdminComStepUp()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/aprovar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_AprovarTreinador_SemStepUp_Retorna403()
    {
        var response = await CriarClienteAdmin()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/aprovar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("step_up_requerido");
    }

    [Fact]
    public async Task Post_AprovarTreinador_NaoEncontrado_Retorna404()
    {
        _factory.AprovarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AprovarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinadorNaoEncontradoException());

        var response = await CriarClienteAdminComStepUp()
            .PostAsJsonAsync($"/admin/treinadores/{Guid.NewGuid()}/aprovar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /admin/treinadores/{id}/inativar ---

    [Fact]
    public async Task Post_InativarTreinador_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/inativar", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_InativarTreinador_Admin_Retorna204()
    {
        _factory.InativarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<InativarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAdminComStepUp()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/inativar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_InativarTreinador_SemStepUp_Retorna403()
    {
        var response = await CriarClienteAdmin()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/inativar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("step_up_requerido");
    }

    // --- DELETE /admin/treinadores/{id} ---

    [Fact]
    public async Task Delete_Treinador_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient()
            .DeleteAsync($"/admin/treinadores/{TreinadorId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Treinador_Admin_Retorna204()
    {
        _factory.ExcluirTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAdmin()
            .DeleteAsync($"/admin/treinadores/{TreinadorId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_Treinador_NaoEncontrado_Retorna404()
    {
        _factory.ExcluirTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinadorNaoEncontradoException());

        var response = await CriarClienteAdmin()
            .DeleteAsync($"/admin/treinadores/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GET /admin/planos ---

    [Fact]
    public async Task Get_Planos_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/admin/planos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Planos_Admin_Retorna200()
    {
        _factory.ListarPlanosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { RespostaPlano });

        var response = await CriarClienteAdmin().GetAsync("/admin/planos");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /admin/planos ---

    [Fact]
    public async Task Post_CriarPlano_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/admin/planos", new { nome = "Pro", maxAlunos = 50, preco = 199m });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_CriarPlano_Admin_Retorna201()
    {
        _factory.CriarPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarPlanoPlataformaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaPlano));

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync("/admin/planos", new { nome = "Pro", maxAlunos = 50, preco = 199m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- GET /admin/grupos-musculares ---

    [Fact]
    public async Task Get_GruposMusculares_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/admin/grupos-musculares");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_GruposMusculares_Admin_Retorna200()
    {
        _factory.ListarGruposHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { RespostaGrupo });

        var response = await CriarClienteAdmin().GetAsync("/admin/grupos-musculares");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /admin/grupos-musculares ---

    [Fact]
    public async Task Post_CriarGrupoMuscular_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin()
            .PostAsJsonAsync("/admin/grupos-musculares", new { nome = "Ombros" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_CriarGrupoMuscular_Admin_Retorna201()
    {
        _factory.CriarGrupoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarGrupoMuscularCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaGrupo));

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync("/admin/grupos-musculares", new { nome = "Ombros" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- PATCH /admin/treinadores/{id}/plano ---

    [Fact]
    public async Task Patch_AtribuirPlano_Admin_Retorna200()
    {
        var atribuido = RespostaTreinador with { PlanoPlataformaId = Guid.NewGuid() };
        _factory.AtribuirPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtribuirPlanoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(atribuido));

        var response = await CriarClienteAdmin()
            .PatchAsJsonAsync($"/admin/treinadores/{TreinadorId}/plano", new { planoId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /admin/alunos ---

    [Fact]
    public async Task Get_Alunos_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/admin/alunos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Alunos_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync("/admin/alunos");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Alunos_Admin_Retorna200()
    {
        _factory.ListarAlunosAdminHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarAlunosAdminQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaListaAlunos);

        var response = await CriarClienteAdmin().GetAsync("/admin/alunos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Get_Alunos_PaginaInvalida_Retorna400()
    {
        var response = await CriarClienteAdmin().GetAsync("/admin/alunos?pagina=0");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- GET /admin/alunos/{id} ---

    [Fact]
    public async Task Get_Aluno_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/alunos/{AlunoId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Aluno_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/alunos/{AlunoId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Aluno_Admin_Retorna200()
    {
        _factory.ObterAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterAlunoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaAluno);

        var response = await CriarClienteAdmin().GetAsync($"/admin/alunos/{AlunoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Aluno_NaoEncontrado_Retorna404()
    {
        _factory.ObterAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterAlunoQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlunoNaoEncontradoException());

        var response = await CriarClienteAdmin().GetAsync($"/admin/alunos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GET /admin/alunos/{id}/vinculo ---

    [Fact]
    public async Task Get_AlunoVinculo_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/alunos/{AlunoId}/vinculo");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AlunoVinculo_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/alunos/{AlunoId}/vinculo");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_AlunoVinculo_Admin_Retorna200()
    {
        _factory.ObterVinculoAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaVinculo);

        var response = await CriarClienteAdmin().GetAsync($"/admin/alunos/{AlunoId}/vinculo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /admin/alunos/{id}/fichas ---

    [Fact]
    public async Task Get_AlunoFichas_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/alunos/{AlunoId}/fichas");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AlunoFichas_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/alunos/{AlunoId}/fichas");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_AlunoFichas_Admin_Retorna200()
    {
        _factory.ListarFichasAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaFichas);

        var response = await CriarClienteAdmin().GetAsync($"/admin/alunos/{AlunoId}/fichas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /admin/fichas/{treinoAlunoId} ---

    [Fact]
    public async Task Get_FichaDetalhe_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/fichas/{TreinoAlunoId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_FichaDetalhe_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/fichas/{TreinoAlunoId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_FichaDetalhe_Admin_Retorna200()
    {
        _factory.ObterFichaAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaFichaDetalhe));

        var response = await CriarClienteAdmin().GetAsync($"/admin/fichas/{TreinoAlunoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_FichaDetalhe_NaoEncontrada_Retorna404()
    {
        _factory.ObterFichaAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<FichaAlunoDetalheResponse>(Error.NotFound("ficha_nao_encontrada", "Ficha não encontrada.")));

        var response = await CriarClienteAdmin().GetAsync($"/admin/fichas/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GET /admin/alunos/{id}/execucoes ---

    [Fact]
    public async Task Get_AlunoExecucoes_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/alunos/{AlunoId}/execucoes");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AlunoExecucoes_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/alunos/{AlunoId}/execucoes");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_AlunoExecucoes_Admin_Retorna200()
    {
        _factory.ListarExecucoesHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaExecucoes);

        var response = await CriarClienteAdmin().GetAsync($"/admin/alunos/{AlunoId}/execucoes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /admin/alunos/{id}/progressao ---

    [Fact]
    public async Task Get_AlunoProgressao_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/alunos/{AlunoId}/progressao");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AlunoProgressao_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/alunos/{AlunoId}/progressao");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_AlunoProgressao_Admin_Retorna200()
    {
        _factory.ObterProgressaoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterProgressaoAlunoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaProgressao);

        var response = await CriarClienteAdmin().GetAsync($"/admin/alunos/{AlunoId}/progressao");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AlunoProgressao_DataInvalida_Retorna400()
    {
        var ontem = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var hoje = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await CriarClienteAdmin()
            .GetAsync($"/admin/alunos/{AlunoId}/progressao?de={hoje}&ate={ontem}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    // --- GET /admin/treinadores/{id}/alunos ---

    [Fact]
    public async Task Get_TreinadorAlunos_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/treinadores/{TreinadorId}/alunos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_TreinadorAlunos_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/treinadores/{TreinadorId}/alunos");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_TreinadorAlunos_Admin_Retorna200()
    {
        _factory.ListarAlunosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarAlunosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaListaAlunos);

        var response = await CriarClienteAdmin().GetAsync($"/admin/treinadores/{TreinadorId}/alunos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /admin/treinadores/{id}/vinculos ---

    [Fact]
    public async Task Get_TreinadorVinculos_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/treinadores/{TreinadorId}/vinculos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_TreinadorVinculos_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/treinadores/{TreinadorId}/vinculos");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_TreinadorVinculos_Admin_Retorna200()
    {
        _factory.ListarVinculosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<VinculoStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaVinculos);

        var response = await CriarClienteAdmin().GetAsync($"/admin/treinadores/{TreinadorId}/vinculos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /admin/treinadores/{id}/treinos ---

    [Fact]
    public async Task Get_TreinadorTreinos_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/treinadores/{TreinadorId}/treinos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_TreinadorTreinos_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/treinadores/{TreinadorId}/treinos");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_TreinadorTreinos_Admin_Retorna200()
    {
        _factory.ListarTreinosDoTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaTreinos);

        var response = await CriarClienteAdmin().GetAsync($"/admin/treinadores/{TreinadorId}/treinos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /admin/treinos/{id} ---

    [Fact]
    public async Task Get_Treino_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/treinos/{TreinoId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Treino_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/treinos/{TreinoId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Treino_Admin_Retorna200()
    {
        _factory.ObterTreinoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterTreinoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaTreino);

        var response = await CriarClienteAdmin().GetAsync($"/admin/treinos/{TreinoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Treino_NaoEncontrado_Retorna404()
    {
        _factory.ObterTreinoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterTreinoQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinoNaoEncontradoException());

        var response = await CriarClienteAdmin().GetAsync($"/admin/treinos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GET /admin/treinadores/{id}/pacotes ---

    [Fact]
    public async Task Get_TreinadorPacotes_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/treinadores/{TreinadorId}/pacotes");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_TreinadorPacotes_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/treinadores/{TreinadorId}/pacotes");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_TreinadorPacotes_Admin_Retorna200()
    {
        _factory.ListarPacotesHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { RespostaPacote });

        var response = await CriarClienteAdmin().GetAsync($"/admin/treinadores/{TreinadorId}/pacotes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /admin/treinadores/{id} ---

    [Fact]
    public async Task Get_ObterTreinador_Admin_Retorna200()
    {
        _factory.ObterTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaTreinador);

        var response = await CriarClienteAdmin().GetAsync($"/admin/treinadores/{TreinadorId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_ObterTreinador_NaoEncontrado_Retorna404()
    {
        _factory.ObterTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinadorNaoEncontradoException());

        var response = await CriarClienteAdmin().GetAsync($"/admin/treinadores/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /admin/treinadores/{id}/reprovar ---

    [Fact]
    public async Task Post_ReprovarTreinador_Admin_Retorna204()
    {
        _factory.ReprovarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ReprovarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAdminComStepUp()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/reprovar", new { observacao = "Reprovado." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_ReprovarTreinador_SemStepUp_Retorna403()
    {
        var response = await CriarClienteAdmin()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/reprovar", new { observacao = "Reprovado." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("step_up_requerido");
    }

    [Fact]
    public async Task Post_ReprovarTreinador_NaoEncontrado_Retorna404()
    {
        _factory.ReprovarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ReprovarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinadorNaoEncontradoException());

        var response = await CriarClienteAdminComStepUp()
            .PostAsJsonAsync($"/admin/treinadores/{Guid.NewGuid()}/reprovar", new { observacao = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /admin/treinadores/{id}/aprovar — failure branch ---

    [Fact]
    public async Task Post_AprovarTreinador_Falha_Retorna422()
    {
        _factory.AprovarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AprovarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<TreinadorResponse>(Error.Business("treinador.ja_aprovado", "Treinador já aprovado.")));

        var response = await CriarClienteAdminComStepUp()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/aprovar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- POST /admin/treinadores/{id}/inativar — failure branch ---

    [Fact]
    public async Task Post_InativarTreinador_Falha_Retorna422()
    {
        _factory.InativarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<InativarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Business("treinador.ja_inativo", "Treinador já inativo.")));

        var response = await CriarClienteAdminComStepUp()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/inativar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- PATCH /admin/planos/{id} ---

    [Fact]
    public async Task Patch_AtualizarPlano_Admin_Retorna200()
    {
        _factory.AtualizarPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarPlanoPlataformaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaPlano));

        var response = await CriarClienteAdmin()
            .PatchAsJsonAsync($"/admin/planos/{Guid.NewGuid()}", new { nome = "Pro Plus", maxAlunos = 20, preco = 199m });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_AtualizarPlano_NaoEncontrado_Retorna404()
    {
        _factory.AtualizarPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarPlanoPlataformaCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlanoPlataformaNaoEncontradoException());

        var response = await CriarClienteAdmin()
            .PatchAsJsonAsync($"/admin/planos/{Guid.NewGuid()}", new { nome = "X" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- DELETE /admin/planos/{id} ---

    [Fact]
    public async Task Delete_Plano_Admin_Retorna204()
    {
        _factory.ExcluirPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirPlanoPlataformaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAdmin()
            .DeleteAsync($"/admin/planos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_Plano_NaoEncontrado_Retorna404()
    {
        _factory.ExcluirPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirPlanoPlataformaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("plano_nao_encontrado", "Plano não encontrado.")));

        var response = await CriarClienteAdmin()
            .DeleteAsync($"/admin/planos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- PATCH /admin/grupos-musculares/{id} ---

    [Fact]
    public async Task Patch_AtualizarGrupoMuscular_Admin_Retorna200()
    {
        _factory.AtualizarGrupoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarGrupoMuscularCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaGrupo));

        var response = await CriarClienteAdmin()
            .PatchAsJsonAsync($"/admin/grupos-musculares/{Guid.NewGuid()}", new { nome = "Ombros" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_AtualizarGrupoMuscular_NaoEncontrado_Retorna404()
    {
        _factory.AtualizarGrupoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarGrupoMuscularCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GrupoMuscularNaoEncontradoException());

        var response = await CriarClienteAdmin()
            .PatchAsJsonAsync($"/admin/grupos-musculares/{Guid.NewGuid()}", new { nome = "X" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- DELETE /admin/grupos-musculares/{id} ---

    [Fact]
    public async Task Delete_GrupoMuscular_Admin_Retorna204()
    {
        _factory.ExcluirGrupoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirGrupoMuscularCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAdmin()
            .DeleteAsync($"/admin/grupos-musculares/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_GrupoMuscular_NaoEncontrado_Retorna404()
    {
        _factory.ExcluirGrupoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirGrupoMuscularCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("grupo_muscular_nao_encontrado", "Grupo muscular não encontrado.")));

        var response = await CriarClienteAdmin()
            .DeleteAsync($"/admin/grupos-musculares/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GET /admin/exercicios ---

    [Fact]
    public async Task Get_Exercicios_Admin_Retorna200()
    {
        _factory.ListarExerciciosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarExerciciosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaExercicios);

        var response = await CriarClienteAdmin().GetAsync("/admin/exercicios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /admin/exercicios ---

    [Fact]
    public async Task Post_CriarExercicioAdmin_Admin_Retorna201()
    {
        _factory.CriarExercicioAdminHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaExercicio));

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync("/admin/exercicios", new { nome = "Supino", grupoMuscularId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_CriarExercicioAdmin_NomeDuplicado_Retorna422()
    {
        _factory.CriarExercicioAdminHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ExercicioResponse>(Error.Business("exercicio.nome_duplicado", "Já existe um exercício com este nome nesta biblioteca.")));

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync("/admin/exercicios", new { nome = "Supino", grupoMuscularId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- PATCH /admin/exercicios/{id} ---

    [Fact]
    public async Task Patch_AtualizarExercicioAdmin_Admin_Retorna200()
    {
        _factory.AtualizarExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaExercicio));

        var response = await CriarClienteAdmin()
            .PatchAsJsonAsync($"/admin/exercicios/{Guid.NewGuid()}", new { nome = "Supino Inclinado" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_AtualizarExercicioAdmin_NomeDuplicado_Retorna422()
    {
        _factory.AtualizarExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ExercicioResponse>(Error.Business("exercicio.nome_duplicado", "Já existe um exercício com este nome nesta biblioteca.")));

        var response = await CriarClienteAdmin()
            .PatchAsJsonAsync($"/admin/exercicios/{Guid.NewGuid()}", new { nome = "Supino" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- DELETE /admin/exercicios/{id} ---

    [Fact]
    public async Task Delete_ExercicioAdmin_Admin_Retorna204()
    {
        _factory.ExcluirExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAdmin()
            .DeleteAsync($"/admin/exercicios/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ExercicioAdmin_EmUso_Retorna422()
    {
        _factory.ExcluirExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Business("exercicio.em_uso", "Este exercício está em uso em fichas de treino e não pode ser excluído.")));

        var response = await CriarClienteAdmin()
            .DeleteAsync($"/admin/exercicios/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- GET /admin/contas/{id}/lgpd/exportar ---

    [Fact]
    public async Task Get_ExportarLgpdAdmin_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync($"/admin/contas/{AlunoId}/lgpd/exportar");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ExportarLgpdAdmin_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync($"/admin/contas/{AlunoId}/lgpd/exportar");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_ExportarLgpdAdmin_FormatoXlsx_Retorna200ComArquivoXlsx()
    {
        var export = CriarExportFake();
        var fakeBytes = new byte[] { 1, 2, 3 };

        _factory.ExportarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExportarDadosPessoaisCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(export));
        _factory.ExcelRendererMock
            .Setup(r => r.Render(It.IsAny<DadosPessoaisExport>()))
            .Returns(fakeBytes);

        var response = await CriarClienteAdmin().GetAsync($"/admin/contas/{AlunoId}/lgpd/exportar?formato=xlsx");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("meus-dados.xlsx");
    }

    [Fact]
    public async Task Get_ExportarLgpdAdmin_SemFormato_Retorna200Json()
    {
        var export = CriarExportFake();
        _factory.ExportarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExportarDadosPessoaisCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(export));

        var response = await CriarClienteAdmin().GetAsync($"/admin/contas/{AlunoId}/lgpd/exportar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Get_NotasFiscais_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/admin/notas-fiscais");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_NotasFiscais_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync("/admin/notas-fiscais");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_NotasFiscais_Admin_Retorna200()
    {
        var item = new NotaFiscalAdminResponse(
            Guid.NewGuid(), TreinadorId, TipoNotaFiscal.AssinaturaSaaS, NotaFiscalStatus.Emitida,
            99m, null, null, "123", "CHV-1", DateTime.UtcNow, null, null, DateTime.UtcNow);
        _factory.ListarNotasFiscaisAdminHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<NotaFiscalStatus?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListarNotasFiscaisAdminResponse([item], null));

        var response = await CriarClienteAdmin().GetAsync("/admin/notas-fiscais?status=Emitida");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_Reprocessar_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin()
            .PostAsJsonAsync($"/admin/notas-fiscais/{Guid.NewGuid()}/reprocessar", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Reprocessar_Admin_Retorna204()
    {
        _factory.ReprocessarNotaFiscalHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync($"/admin/notas-fiscais/{Guid.NewGuid()}/reprocessar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_Reprocessar_NaoEncontrada_Retorna404()
    {
        _factory.ReprocessarNotaFiscalHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("nota_fiscal.nao_encontrada", "Nota fiscal não encontrada.")));

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync($"/admin/notas-fiscais/{Guid.NewGuid()}/reprocessar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_Reprocessar_StatusInvalido_Retorna422()
    {
        _factory.ReprocessarNotaFiscalHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Business("nota_fiscal.reprocessamento_invalido", "Apenas notas fiscais em erro podem ser reprocessadas.")));

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync($"/admin/notas-fiscais/{Guid.NewGuid()}/reprocessar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static DadosPessoaisExport CriarExportFake() =>
        new("1.0", DateTime.UtcNow,
            new ContaExportDto(AlunoId, "user@test.com", "Aluno", false, null, DateTime.UtcNow),
            null, null, [], [], [], [], [], [], [], []);

    // --- WebApplicationFactory ---

    public class AdminWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ListarTreinadoresHandler> ListarTreinadoresHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>());

        public Mock<AprovarTreinadorHandler> AprovarTreinadorHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<AprovarTreinadorHandler>>());

        public Mock<ReprovarTreinadorHandler> ReprovarTreinadorHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<ReprovarTreinadorHandler>>());

        public Mock<InativarTreinadorHandler> InativarTreinadorHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IPacoteRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<InativarTreinadorHandler>>());

        public Mock<ExcluirTreinadorHandler> ExcluirTreinadorHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<ILogger<ExcluirTreinadorHandler>>());

        public Mock<AtribuirPlanoHandler> AtribuirPlanoHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IPlanoPlataformaRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<AtribuirPlanoHandler>>());

        public Mock<ListarPlanosPlataformaHandler> ListarPlanosHandlerMock { get; } = new(
            Mock.Of<IPlanoPlataformaRepository>());

        public Mock<CriarPlanoPlataformaHandler> CriarPlanoHandlerMock { get; } = new(
            Mock.Of<IPlanoPlataformaRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<CriarPlanoPlataformaCommand>>(),
            Mock.Of<IUserContext>(), TimeProvider.System,
            Mock.Of<ILogger<CriarPlanoPlataformaHandler>>());

        public Mock<ListarGruposMuscularesHandler> ListarGruposHandlerMock { get; } = new(
            Mock.Of<IGrupoMuscularRepository>());

        public Mock<CriarGrupoMuscularHandler> CriarGrupoHandlerMock { get; } = new(
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<CriarGrupoMuscularCommand>>(), TimeProvider.System);

        public Mock<ListarAlunosAdminHandler> ListarAlunosAdminHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>());

        public Mock<ObterAlunoHandler> ObterAlunoHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<ObterAlunoHandler>>());

        public Mock<ObterVinculoAlunoHandler> ObterVinculoAlunoHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<ITreinadorRepository>());

        public Mock<ListarFichasAlunoHandler> ListarFichasAlunoHandlerMock { get; } = new(
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IExercicioRepository>());

        public Mock<ObterFichaAlunoHandler> ObterFichaAlunoHandlerMock { get; } = new(
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IUserContext>());

        public Mock<ListarExecucoesAlunoHandler> ListarExecucoesHandlerMock { get; } = new(
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IUserContext>());

        public Mock<ObterProgressaoAlunoHandler> ObterProgressaoHandlerMock { get; } = new(
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IUserContext>());

        public Mock<ListarAlunosHandler> ListarAlunosHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<ListarAlunosHandler>>());

        public Mock<ListarVinculosHandler> ListarVinculosHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>());

        public Mock<ListarTreinosDoTreinadorHandler> ListarTreinosDoTreinadorHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExercicioRepository>());

        public Mock<ObterTreinoHandler> ObterTreinoHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExercicioRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<ObterTreinoHandler>>());

        public Mock<ListarPacotesHandler> ListarPacotesHandlerMock { get; } = new(
            Mock.Of<IPacoteRepository>());

        public Mock<ObterTreinadorHandler> ObterTreinadorHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>());

        public Mock<AtualizarPlanoPlataformaHandler> AtualizarPlanoHandlerMock { get; } = new(
            Mock.Of<IPlanoPlataformaRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<AtualizarPlanoPlataformaCommand>>(), TimeProvider.System);

        public Mock<ExcluirPlanoPlataformaHandler> ExcluirPlanoHandlerMock { get; } = new(
            Mock.Of<IPlanoPlataformaRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System);

        public Mock<AtualizarGrupoMuscularHandler> AtualizarGrupoHandlerMock { get; } = new(
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<IValidator<AtualizarGrupoMuscularCommand>>());

        public Mock<ExcluirGrupoMuscularHandler> ExcluirGrupoHandlerMock { get; } = new(
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IUnitOfWork>());

        public Mock<ListarExerciciosHandler> ListarExerciciosHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<ILogger<ListarExerciciosHandler>>());

        public Mock<CriarExercicioHandler> CriarExercicioAdminHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<CriarExercicioCommand>>(), TimeProvider.System,
            Mock.Of<ILogger<CriarExercicioHandler>>());

        public Mock<AtualizarExercicioHandler> AtualizarExercicioHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System);

        public Mock<ExcluirExercicioHandler> ExcluirExercicioHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IUnitOfWork>());

        public Mock<ExportarDadosPessoaisHandler> ExportarHandlerMock { get; } = new(
            Mock.Of<IContaRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IAssinaturaAlunoRepository>(),
            Mock.Of<IPagamentoRepository>(),
            Mock.Of<IPacoteRepository>(),
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IEmailDeliveryLogRepository>(),
            Mock.Of<IWhatsAppDeliveryLogRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System);

        public Mock<IDadosPessoaisExcelRenderer> ExcelRendererMock { get; } = new();

        public Mock<ListarNotasFiscaisAdminHandler> ListarNotasFiscaisAdminHandlerMock { get; } = new(
            Mock.Of<INotaFiscalRepository>());

        public Mock<ReprocessarNotaFiscalHandler> ReprocessarNotaFiscalHandlerMock { get; } = new(
            Mock.Of<INotaFiscalRepository>(),
            Mock.Of<IOutboxEnfileirador>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<ReprocessarNotaFiscalHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ListarTreinadoresHandler>();
                services.RemoveAll<AprovarTreinadorHandler>();
                services.RemoveAll<ReprovarTreinadorHandler>();
                services.RemoveAll<InativarTreinadorHandler>();
                services.RemoveAll<ExcluirTreinadorHandler>();
                services.RemoveAll<AtribuirPlanoHandler>();
                services.RemoveAll<ListarPlanosPlataformaHandler>();
                services.RemoveAll<CriarPlanoPlataformaHandler>();
                services.RemoveAll<ListarGruposMuscularesHandler>();
                services.RemoveAll<CriarGrupoMuscularHandler>();
                services.RemoveAll<ListarAlunosAdminHandler>();
                services.RemoveAll<ObterAlunoHandler>();
                services.RemoveAll<ObterVinculoAlunoHandler>();
                services.RemoveAll<ListarFichasAlunoHandler>();
                services.RemoveAll<ObterFichaAlunoHandler>();
                services.RemoveAll<ListarExecucoesAlunoHandler>();
                services.RemoveAll<ObterProgressaoAlunoHandler>();
                services.RemoveAll<ListarAlunosHandler>();
                services.RemoveAll<ListarVinculosHandler>();
                services.RemoveAll<ListarTreinosDoTreinadorHandler>();
                services.RemoveAll<ObterTreinoHandler>();
                services.RemoveAll<ListarPacotesHandler>();
                services.RemoveAll<ObterTreinadorHandler>();
                services.RemoveAll<AtualizarPlanoPlataformaHandler>();
                services.RemoveAll<ExcluirPlanoPlataformaHandler>();
                services.RemoveAll<AtualizarGrupoMuscularHandler>();
                services.RemoveAll<ExcluirGrupoMuscularHandler>();
                services.RemoveAll<ListarExerciciosHandler>();
                services.RemoveAll<CriarExercicioHandler>();
                services.RemoveAll<AtualizarExercicioHandler>();
                services.RemoveAll<ExcluirExercicioHandler>();
                services.RemoveAll<ExportarDadosPessoaisHandler>();
                services.RemoveAll<IDadosPessoaisExcelRenderer>();
                services.RemoveAll<ListarNotasFiscaisAdminHandler>();
                services.RemoveAll<ReprocessarNotaFiscalHandler>();
                services.RemoveAll<IUserContext>();
                services.RemoveAll<IJwtService>();
                services.RemoveAll<ITokenRevogadoRepository>();

                services.AddScoped(_ => ListarTreinadoresHandlerMock.Object);
                services.AddScoped(_ => AprovarTreinadorHandlerMock.Object);
                services.AddScoped(_ => ReprovarTreinadorHandlerMock.Object);
                services.AddScoped(_ => InativarTreinadorHandlerMock.Object);
                services.AddScoped(_ => ExcluirTreinadorHandlerMock.Object);
                services.AddScoped(_ => AtribuirPlanoHandlerMock.Object);
                services.AddScoped(_ => ListarPlanosHandlerMock.Object);
                services.AddScoped(_ => CriarPlanoHandlerMock.Object);
                services.AddScoped(_ => ListarGruposHandlerMock.Object);
                services.AddScoped(_ => CriarGrupoHandlerMock.Object);
                services.AddScoped(_ => ListarAlunosAdminHandlerMock.Object);
                services.AddScoped(_ => ObterAlunoHandlerMock.Object);
                services.AddScoped(_ => ObterVinculoAlunoHandlerMock.Object);
                services.AddScoped(_ => ListarFichasAlunoHandlerMock.Object);
                services.AddScoped(_ => ObterFichaAlunoHandlerMock.Object);
                services.AddScoped(_ => ListarExecucoesHandlerMock.Object);
                services.AddScoped(_ => ObterProgressaoHandlerMock.Object);
                services.AddScoped(_ => ListarAlunosHandlerMock.Object);
                services.AddScoped(_ => ListarVinculosHandlerMock.Object);
                services.AddScoped(_ => ListarTreinosDoTreinadorHandlerMock.Object);
                services.AddScoped(_ => ObterTreinoHandlerMock.Object);
                services.AddScoped(_ => ListarPacotesHandlerMock.Object);
                services.AddScoped(_ => ObterTreinadorHandlerMock.Object);
                services.AddScoped(_ => AtualizarPlanoHandlerMock.Object);
                services.AddScoped(_ => ExcluirPlanoHandlerMock.Object);
                services.AddScoped(_ => AtualizarGrupoHandlerMock.Object);
                services.AddScoped(_ => ExcluirGrupoHandlerMock.Object);
                services.AddScoped(_ => ListarExerciciosHandlerMock.Object);
                services.AddScoped(_ => CriarExercicioAdminHandlerMock.Object);
                services.AddScoped(_ => AtualizarExercicioHandlerMock.Object);
                services.AddScoped(_ => ExcluirExercicioHandlerMock.Object);
                services.AddScoped(_ => ExportarHandlerMock.Object);
                services.AddScoped<IDadosPessoaisExcelRenderer>(_ => ExcelRendererMock.Object);
                services.AddScoped(_ => ListarNotasFiscaisAdminHandlerMock.Object);
                services.AddScoped(_ => ReprocessarNotaFiscalHandlerMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.ContaId).Returns(AdminId);
                userContextMock.Setup(u => u.PerfilId).Returns(AdminId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.SystemAdmin);
                services.AddScoped(_ => userContextMock.Object);

                var jwtMock = new Mock<IJwtService>();
                jwtMock.Setup(j => j.ValidarTokenEscopo("step-up-ok", MfaScopes.StepUp))
                    .Returns(new EscopoValidado(AdminId, Guid.NewGuid()));
                services.AddScoped(_ => jwtMock.Object);

                var tokenRevogadoMock = new Mock<ITokenRevogadoRepository>();
                tokenRevogadoMock.Setup(r => r.EstaRevogadoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);
                services.AddScoped(_ => tokenRevogadoMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, AdminTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class AdminTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AdminTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var param = header.Replace("Test ", "");
            if (string.IsNullOrEmpty(param))
                return Task.FromResult(AuthenticateResult.Fail("Token inválido"));

            string userId;
            string tipoConta;

            if (param == "admin")
            {
                userId = AdminId.ToString();
                tipoConta = "SystemAdmin";
            }
            else if (param == "treinador")
            {
                userId = Guid.NewGuid().ToString();
                tipoConta = "Treinador";
            }
            else
            {
                return Task.FromResult(AuthenticateResult.Fail("Token inválido"));
            }

            var claims = new[]
            {
                new Claim("sub", userId),
                new Claim("tipo_conta", tipoConta)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
