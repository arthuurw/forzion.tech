using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos;
using forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;
using forzion.tech.Application.UseCases.Treinos.AtualizarTreino;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Application.UseCases.Treinos.DuplicarTreino;
using forzion.tech.Application.UseCases.Treinos.ExcluirTreino;
using forzion.tech.Application.UseCases.Treinos.ListarAlunosTreino;
using forzion.tech.Application.UseCases.Treinos.ObterTreino;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;
using forzion.tech.Application.UseCases.Treinos.RemoverExercicio;
using forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;
using forzion.tech.Domain.Shared;
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

namespace forzion.tech.Tests.Api.Endpoints;

public class TreinoEndpointsTests : IClassFixture<TreinoEndpointsTests.TreinoWebFactory>
{
    private readonly TreinoWebFactory _factory;
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TreinoId = Guid.NewGuid();

    private static readonly TreinoResponse TreinoResp = new(
        TreinoId, "Treino A", ObjetivoTreino.Hipertrofia, DificuldadeTreino.Iniciante,
        null, null, TreinadorId, [], DateTime.UtcNow, null);

    public TreinoEndpointsTests(TreinoWebFactory factory) => _factory = factory;

    private HttpClient CriarClienteAutenticado()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", UserId.ToString());
        return client;
    }

    // --- POST /treinos ---

    [Fact]
    public async Task Post_Criar_Retorna201()
    {
        _factory.CriarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinoResp);

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/treinos",
            new { alunoId = Guid.NewGuid(), nome = "Treino A", objetivo = ObjetivoTreino.Hipertrofia });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_Criar_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/treinos",
            new { nome = "X", objetivo = ObjetivoTreino.Hipertrofia });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Criar_DomainException_Retorna422()
    {
        _factory.CriarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Aluno indisponível."));

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/treinos",
            new { alunoId = Guid.NewGuid(), nome = "Treino A", objetivo = ObjetivoTreino.Hipertrofia });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- GET /treinos/{id} ---

    [Fact]
    public async Task Get_Obter_Retorna200()
    {
        _factory.ObterHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterTreinoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinoResp);

        var response = await CriarClienteAutenticado().GetAsync($"/treinos/{TreinoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Obter_NaoEncontrado_Retorna404()
    {
        _factory.ObterHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterTreinoQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinoNaoEncontradoException());

        var response = await CriarClienteAutenticado().GetAsync($"/treinos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Obter_TreinoDeOutroTreinador_Retorna403()
    {
        _factory.ObterHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterTreinoQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcessoNegadoException());

        var response = await CriarClienteAutenticado().GetAsync($"/treinos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- PATCH /treinos/{id} ---

    [Fact]
    public async Task Patch_Atualizar_Retorna200()
    {
        _factory.AtualizarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(TreinoResp));

        var response = await CriarClienteAutenticado().PatchAsJsonAsync(
            $"/treinos/{TreinoId}", new { nome = "Treino B" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_Atualizar_NaoEncontrado_Retorna404()
    {
        _factory.AtualizarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinoNaoEncontradoException());

        var response = await CriarClienteAutenticado().PatchAsJsonAsync(
            $"/treinos/{Guid.NewGuid()}", new { nome = "X" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- DELETE /treinos/{id} ---

    [Fact]
    public async Task Delete_Excluir_Retorna204()
    {
        _factory.ExcluirHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAutenticado().DeleteAsync($"/treinos/{TreinoId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_Excluir_AcessoNegado_Retorna403()
    {
        _factory.ExcluirHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcessoNegadoException());

        var response = await CriarClienteAutenticado().DeleteAsync($"/treinos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- POST /treinos/{id}/vincular-aluno ---

    [Fact]
    public async Task Post_VincularAluno_Retorna204()
    {
        _factory.VincularFichaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<VincularFichaAoAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAutenticado().PostAsJsonAsync(
            $"/treinos/{TreinoId}/vincular-aluno", new { alunoId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_VincularAluno_AcessoNegado_Retorna403()
    {
        _factory.VincularFichaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<VincularFichaAoAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcessoNegadoException());

        var response = await CriarClienteAutenticado().PostAsJsonAsync(
            $"/treinos/{Guid.NewGuid()}/vincular-aluno", new { alunoId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- POST /treinos/{id}/exercicios ---

    [Fact]
    public async Task Post_AdicionarExercicio_Retorna200()
    {
        _factory.AdicionarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AdicionarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(TreinoResp));

        var response = await CriarClienteAutenticado().PostAsJsonAsync($"/treinos/{TreinoId}/exercicios",
            new { exercicioId = Guid.NewGuid(), series = new[] { new { quantidade = 3, repeticoesMin = 10, repeticoesMax = (int?)12, descricao = (string?)null, carga = (decimal?)null, descanso = (int?)60 } } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_AdicionarExercicio_TreinoDeOutroTreinador_Retorna403()
    {
        _factory.AdicionarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AdicionarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcessoNegadoException());

        var response = await CriarClienteAutenticado().PostAsJsonAsync($"/treinos/{Guid.NewGuid()}/exercicios",
            new { exercicioId = Guid.NewGuid(), series = new[] { new { quantidade = 3, repeticoesMin = 10, repeticoesMax = (int?)12, descricao = (string?)null, carga = (decimal?)null, descanso = (int?)60 } } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- DELETE /treinos/{id}/exercicios/{treinoExercicioId} ---

    [Fact]
    public async Task Delete_RemoverExercicio_Retorna200()
    {
        _factory.RemoverExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<RemoverExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(TreinoResp));

        var response = await CriarClienteAutenticado()
            .DeleteAsync($"/treinos/{TreinoId}/exercicios/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_RemoverExercicio_TreinoExecutado_Retorna422()
    {
        _factory.RemoverExercicioHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<RemoverExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinoExecutadoException());

        var response = await CriarClienteAutenticado()
            .DeleteAsync($"/treinos/{TreinoId}/exercicios/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- POST /treinos/{id}/duplicar ---

    [Fact]
    public async Task Post_Duplicar_Retorna201()
    {
        _factory.DuplicarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<DuplicarTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinoResp);

        var response = await CriarClienteAutenticado().PostAsJsonAsync(
            $"/treinos/{TreinoId}/duplicar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_Duplicar_NaoEncontrado_Retorna404()
    {
        _factory.DuplicarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<DuplicarTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinoNaoEncontradoException());

        var response = await CriarClienteAutenticado().PostAsJsonAsync(
            $"/treinos/{Guid.NewGuid()}/duplicar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GET /treinos/{id}/alunos ---

    [Fact]
    public async Task Get_AlunosTreino_Retorna200()
    {
        _factory.ListarAlunosTreinoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarAlunosTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TreinoAlunoVinculado>());

        var response = await CriarClienteAutenticado().GetAsync($"/treinos/{TreinoId}/alunos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /treinos/{id}/execucoes ---

    [Fact]
    public async Task Post_RegistrarExecucao_Retorna201()
    {
        var execResp = new RegistrarExecucaoResponse(
            Guid.NewGuid(), TreinoId, Guid.NewGuid(), DateTime.UtcNow, null, DateTime.UtcNow);
        _factory.RegistrarExecucaoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<RegistrarExecucaoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(execResp);

        var response = await CriarClienteAutenticado().PostAsJsonAsync($"/treinos/{TreinoId}/execucoes",
            new { alunoId = Guid.NewGuid(), dataExecucao = DateTime.UtcNow, observacao = (string?)null, exercicios = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_RegistrarExecucao_VinculoInativo_Retorna403()
    {
        _factory.RegistrarExecucaoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<RegistrarExecucaoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcessoNegadoException());

        var response = await CriarClienteAutenticado().PostAsJsonAsync($"/treinos/{TreinoId}/execucoes",
            new { alunoId = Guid.NewGuid(), dataExecucao = DateTime.UtcNow, observacao = (string?)null, exercicios = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- WebApplicationFactory ---

    public class TreinoWebFactory : WebApplicationFactory<Program>
    {
        private static readonly IValidator<CriarTreinoCommand> CriarValidator =
            new CriarTreinoCommandValidator();

        private static readonly IValidator<AdicionarExercicioCommand> AdicionarValidator =
            new AdicionarExercicioCommandValidator();

        public Mock<CriarTreinoHandler> CriarHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(),
            CriarValidator, TimeProvider.System,
            Mock.Of<ILogger<CriarTreinoHandler>>())
        { CallBase = true };

        public Mock<ObterTreinoHandler> ObterHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExercicioRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<ObterTreinoHandler>>());

        public Mock<AtualizarTreinoHandler> AtualizarHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(), TimeProvider.System,
            Mock.Of<ILogger<AtualizarTreinoHandler>>());

        public Mock<ExcluirTreinoHandler> ExcluirHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<ExcluirTreinoHandler>>());

        public Mock<AdicionarExercicioHandler> AdicionarHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(),
            AdicionarValidator, TimeProvider.System,
            Mock.Of<ILogger<AdicionarExercicioHandler>>())
        { CallBase = true };

        public Mock<RemoverExercicioHandler> RemoverExercicioHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(), TimeProvider.System,
            Mock.Of<ILogger<RemoverExercicioHandler>>());

        public Mock<DuplicarTreinoHandler> DuplicarHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(), TimeProvider.System,
            Mock.Of<ILogger<DuplicarTreinoHandler>>());

        public Mock<VincularFichaAoAlunoHandler> VincularFichaHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(), TimeProvider.System,
            Mock.Of<ILogger<VincularFichaAoAlunoHandler>>());

        public Mock<ListarAlunosTreinoHandler> ListarAlunosTreinoHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IUserContext>());

        public Mock<RegistrarExecucaoHandler> RegistrarExecucaoHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(), TimeProvider.System,
            Mock.Of<ILogger<RegistrarExecucaoHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<CriarTreinoHandler>();
                services.RemoveAll<ObterTreinoHandler>();
                services.RemoveAll<AtualizarTreinoHandler>();
                services.RemoveAll<ExcluirTreinoHandler>();
                services.RemoveAll<AdicionarExercicioHandler>();
                services.RemoveAll<RemoverExercicioHandler>();
                services.RemoveAll<DuplicarTreinoHandler>();
                services.RemoveAll<VincularFichaAoAlunoHandler>();
                services.RemoveAll<ListarAlunosTreinoHandler>();
                services.RemoveAll<RegistrarExecucaoHandler>();

                services.AddScoped(_ => CriarHandlerMock.Object);
                services.AddScoped(_ => ObterHandlerMock.Object);
                services.AddScoped(_ => AtualizarHandlerMock.Object);
                services.AddScoped(_ => ExcluirHandlerMock.Object);
                services.AddScoped(_ => AdicionarHandlerMock.Object);
                services.AddScoped(_ => RemoverExercicioHandlerMock.Object);
                services.AddScoped(_ => DuplicarHandlerMock.Object);
                services.AddScoped(_ => VincularFichaHandlerMock.Object);
                services.AddScoped(_ => ListarAlunosTreinoHandlerMock.Object);
                services.AddScoped(_ => RegistrarExecucaoHandlerMock.Object);

                services.RemoveAll<IUserContext>();
                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.PerfilId).Returns(TreinadorId);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var userId = header.Replace("Test ", "");
            var claims = new[] { new Claim("sub", userId) };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
