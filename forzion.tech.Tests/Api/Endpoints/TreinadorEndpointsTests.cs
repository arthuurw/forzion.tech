using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Exercicios;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pacotes.AtualizarPacoteAluno;
using forzion.tech.Application.UseCases.Pacotes.CriarPacoteAluno;
using forzion.tech.Application.UseCases.Pacotes.ExcluirPacoteAluno;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotesAluno;
using forzion.tech.Application.UseCases.Treinos.ListarTreinosDoTreinador;
using forzion.tech.Application.UseCases.Vinculos;
using forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;
using forzion.tech.Application.UseCases.Vinculos.DesvincularAluno;
using forzion.tech.Application.UseCases.Vinculos.ListarVinculos;
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

public class TreinadorEndpointsTests : IClassFixture<TreinadorEndpointsTests.TreinadorWebFactory>
{
    private readonly TreinadorWebFactory _factory;

    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid VinculoId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();

    private static readonly AlunoResponse RespostaAluno = new(
        AlunoId, "João", null, null, AlunoStatus.Ativo, ContaId, DateTime.UtcNow, null);

    private static readonly ListarAlunosResponse RespostaListaAlunos =
        new([RespostaAluno], 1, 1, 20);

    private static readonly VinculoResponse RespostaVinculo = new(
        VinculoId, TreinadorId, AlunoId, PacoteId, VinculoStatus.Ativo, DateTime.UtcNow);

    private static readonly forzion.tech.Application.UseCases.Vinculos.ListarVinculos.VinculoDetalheResponse RespostaVinculoDetalhe = new(
        VinculoId, TreinadorId, AlunoId, PacoteId, VinculoStatus.Ativo, "João", null, DateTime.UtcNow, false);

    private static readonly PacoteAlunoResponse RespostaPacote = new(
        PacoteId, TreinadorId, "Pacote Básico", null, 99m, true, DateTime.UtcNow, null);

    public TreinadorEndpointsTests(TreinadorWebFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CriarClienteTreinador()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "treinador");
        return client;
    }

    private HttpClient CriarClienteAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "admin");
        return client;
    }

    // --- Auth ---

    [Fact]
    public async Task Get_Alunos_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/treinador/alunos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Alunos_RoleErrada_Retorna403()
    {
        var response = await CriarClienteAdmin().GetAsync("/treinador/alunos");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GET /treinador/alunos ---

    [Fact]
    public async Task Get_Alunos_Treinador_Retorna200()
    {
        _factory.ListarAlunosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarAlunosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaListaAlunos);

        var response = await CriarClienteTreinador().GetAsync("/treinador/alunos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /treinador/vinculos/{id}/aprovar ---

    [Fact]
    public async Task Post_AprovarVinculo_Treinador_Retorna200()
    {
        _factory.AprovarVinculoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AprovarVinculoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaVinculo);

        var response = await CriarClienteTreinador().PostAsJsonAsync(
            $"/treinador/vinculos/{VinculoId}/aprovar",
            new { PacoteAlunoId = PacoteId, TrarFichas = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_AprovarVinculo_NaoEncontrado_Retorna404()
    {
        _factory.AprovarVinculoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AprovarVinculoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlunoNaoEncontradoException());

        var response = await CriarClienteTreinador().PostAsJsonAsync(
            $"/treinador/vinculos/{Guid.NewGuid()}/aprovar",
            new { PacoteAlunoId = Guid.NewGuid(), TrarFichas = false });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /treinador/vinculos/{id}/desvincular ---

    [Fact]
    public async Task Post_DesvincularAluno_Treinador_Retorna204()
    {
        _factory.DesvincularAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<DesvincularAlunoCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await CriarClienteTreinador().PostAsJsonAsync(
            $"/treinador/vinculos/{VinculoId}/desvincular",
            new { Observacao = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- GET /treinador/vinculos ---

    [Fact]
    public async Task Get_Vinculos_Treinador_Retorna200()
    {
        _factory.ListarVinculosHandlerMock
            .Setup(h => h.HandleAsync(
                It.IsAny<Guid>(), It.IsAny<VinculoStatus?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListarVinculosResponse([RespostaVinculoDetalhe], 1, 1, 20));

        var response = await CriarClienteTreinador().GetAsync("/treinador/vinculos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /treinador/treinos ---

    [Fact]
    public async Task Get_Treinos_Treinador_Retorna200()
    {
        _factory.ListarTreinosHandlerMock
            .Setup(h => h.HandleAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new forzion.tech.Application.UseCases.Treinos.ListarTreinos.ListarTreinosResponse([], 0, 1, 20));

        var response = await CriarClienteTreinador().GetAsync("/treinador/treinos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /treinador/exercicios ---

    [Fact]
    public async Task Get_Exercicios_Treinador_Retorna200()
    {
        _factory.ListarExerciciosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarExerciciosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListarExerciciosResponse([], 0, 1, 20));

        var response = await CriarClienteTreinador().GetAsync("/treinador/exercicios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /treinador/pacotes ---

    [Fact]
    public async Task Get_Pacotes_Treinador_Retorna200()
    {
        _factory.ListarPacotesHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([RespostaPacote]);

        var response = await CriarClienteTreinador().GetAsync("/treinador/pacotes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /treinador/pacotes ---

    [Fact]
    public async Task Post_CriarPacote_Treinador_Retorna201()
    {
        _factory.CriarPacoteHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarPacoteAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaPacote);

        var response = await CriarClienteTreinador().PostAsJsonAsync("/treinador/pacotes",
            new { Nome = "Pacote Premium", Preco = 199m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- PATCH /treinador/pacotes/{id} ---

    [Fact]
    public async Task Patch_AtualizarPacote_Treinador_Retorna200()
    {
        _factory.AtualizarPacoteHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarPacoteAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaPacote with { Nome = "Pacote Atualizado" });

        var response = await CriarClienteTreinador().PatchAsJsonAsync(
            $"/treinador/pacotes/{PacoteId}",
            new { Nome = "Pacote Atualizado", Preco = (decimal?)null, Descricao = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- DELETE /treinador/pacotes/{id} ---

    [Fact]
    public async Task Delete_ExcluirPacote_Treinador_Retorna204()
    {
        _factory.ExcluirPacoteHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirPacoteAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteTreinador().DeleteAsync($"/treinador/pacotes/{PacoteId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ExcluirPacote_NaoEncontrado_Retorna404()
    {
        _factory.ExcluirPacoteHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirPacoteAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PacoteNaoEncontradoException());

        var response = await CriarClienteTreinador().DeleteAsync($"/treinador/pacotes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- WebApplicationFactory ---

    public class TreinadorWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ListarAlunosHandler> ListarAlunosHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ILogger<ListarAlunosHandler>>());

        public Mock<AprovarVinculoHandler> AprovarVinculoHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ILimiteTreinadorService>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IDbContextTransactionProvider>(),
            Mock.Of<IWhatsAppNotifier>(),
            Mock.Of<ILogger<AprovarVinculoHandler>>());

        public Mock<DesvincularAlunoHandler> DesvincularAlunoHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<DesvincularAlunoHandler>>());

        public Mock<ListarVinculosHandler> ListarVinculosHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>());

        public Mock<ListarTreinosDoTreinadorHandler> ListarTreinosHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExercicioRepository>());

        public Mock<ListarExerciciosHandler> ListarExerciciosHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<ILogger<ListarExerciciosHandler>>());

        public Mock<ListarPacotesAlunoHandler> ListarPacotesHandlerMock { get; } = new(
            Mock.Of<IPacoteAlunoRepository>());

        public Mock<CriarPacoteAlunoHandler> CriarPacoteHandlerMock { get; } = new(
            Mock.Of<IPacoteAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<CriarPacoteAlunoCommand>>(),
            Mock.Of<ILogger<CriarPacoteAlunoHandler>>());

        public Mock<AtualizarPacoteAlunoHandler> AtualizarPacoteHandlerMock { get; } = new(
            Mock.Of<IPacoteAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<AtualizarPacoteAlunoCommand>>());

        public Mock<ExcluirPacoteAlunoHandler> ExcluirPacoteHandlerMock { get; } = new(
            Mock.Of<IPacoteAlunoRepository>(),
            Mock.Of<IUnitOfWork>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ListarAlunosHandler>();
                services.RemoveAll<AprovarVinculoHandler>();
                services.RemoveAll<DesvincularAlunoHandler>();
                services.RemoveAll<ListarVinculosHandler>();
                services.RemoveAll<ListarTreinosDoTreinadorHandler>();
                services.RemoveAll<ListarExerciciosHandler>();
                services.RemoveAll<ListarPacotesAlunoHandler>();
                services.RemoveAll<CriarPacoteAlunoHandler>();
                services.RemoveAll<AtualizarPacoteAlunoHandler>();
                services.RemoveAll<ExcluirPacoteAlunoHandler>();
                services.RemoveAll<IUserContext>();

                services.AddScoped(_ => ListarAlunosHandlerMock.Object);
                services.AddScoped(_ => AprovarVinculoHandlerMock.Object);
                services.AddScoped(_ => DesvincularAlunoHandlerMock.Object);
                services.AddScoped(_ => ListarVinculosHandlerMock.Object);
                services.AddScoped(_ => ListarTreinosHandlerMock.Object);
                services.AddScoped(_ => ListarExerciciosHandlerMock.Object);
                services.AddScoped(_ => ListarPacotesHandlerMock.Object);
                services.AddScoped(_ => CriarPacoteHandlerMock.Object);
                services.AddScoped(_ => AtualizarPacoteHandlerMock.Object);
                services.AddScoped(_ => ExcluirPacoteHandlerMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.ContaId).Returns(ContaId);
                userContextMock.Setup(u => u.PerfilId).Returns(TreinadorId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.Treinador);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TreinadorTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class TreinadorTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TreinadorTestAuthHandler(
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

            string tipoConta;
            string userId;

            if (param == "treinador")
            {
                tipoConta = "Treinador";
                userId = TreinadorId.ToString();
            }
            else if (param == "admin")
            {
                tipoConta = "SystemAdmin";
                userId = Guid.NewGuid().ToString();
            }
            else
            {
                return Task.FromResult(AuthenticateResult.Fail("Token inválido"));
            }

            var claims = new[]
            {
                new Claim("sub", userId),
                new Claim("tipo_conta", tipoConta),
                new Claim("perfil_id", TreinadorId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
