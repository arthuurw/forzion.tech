using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Vinculos.DefinirPreservacaoVinculo;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

// e2e in-process (sem Docker) do PATCH /treinador/alunos/{vinculoId}/preservar. Repositório e
// UnitOfWork mockados via DI; DefinirPreservacaoVinculoHandler roda REAL (não mockado) para
// exercitar de fato o guard de IDOR (vínculo de outro treinador -> 404, não 403 — FE-04/GRACE-05).
public class DefinirPreservacaoVinculoEndpointTests : IClassFixture<DefinirPreservacaoVinculoEndpointTests.PreservarWebFactory>
{
    private readonly PreservarWebFactory _factory;

    private static readonly Guid TreinadorAutenticadoId = Guid.NewGuid();

    public DefinirPreservacaoVinculoEndpointTests(PreservarWebFactory factory)
    {
        _factory = factory;
        // Fixture (e mocks) é compartilhada por todos os testes da classe (IClassFixture) —
        // limpar invocações a cada teste evita contagem cruzada em Verify(Times.Once).
        _factory.VinculoRepositoryMock.Invocations.Clear();
        _factory.UnitOfWorkMock.Invocations.Clear();
    }

    private HttpClient ClienteTreinador()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "treinador");
        return client;
    }

    private static VinculoTreinadorAluno VinculoAtivo(Guid treinadorId)
    {
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        vinculo.Aprovar(treinadorId, Guid.NewGuid(), DateTime.UtcNow);
        return vinculo;
    }

    [Fact]
    public async Task Patch_Preservar_VinculoDeOutroTreinador_Retorna404()
    {
        var vinculoDeOutro = VinculoAtivo(Guid.NewGuid());
        _factory.VinculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(vinculoDeOutro.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculoDeOutro);

        var response = await ClienteTreinador().PatchAsJsonAsync(
            $"/treinador/alunos/{vinculoDeOutro.Id}/preservar", new { preservar = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _factory.UnitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Patch_Preservar_VinculoInexistente_Retorna404()
    {
        var id = Guid.NewGuid();
        _factory.VinculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var response = await ClienteTreinador().PatchAsJsonAsync(
            $"/treinador/alunos/{id}/preservar", new { preservar = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_Preservar_VinculoProprio_Retorna200EPersiste()
    {
        var vinculo = VinculoAtivo(TreinadorAutenticadoId);
        _factory.VinculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);

        var response = await ClienteTreinador().PatchAsJsonAsync(
            $"/treinador/alunos/{vinculo.Id}/preservar", new { preservar = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DefinirPreservacaoVinculoResponse>();
        body!.PreservarNoLimite.Should().BeTrue();
        vinculo.PreservarNoLimite.Should().BeTrue();
        _factory.UnitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Patch_Preservar_DesmarcaVinculoProprio_Retorna200ComFalse()
    {
        var vinculo = VinculoAtivo(TreinadorAutenticadoId);
        vinculo.DefinirPreservacao(true, DateTime.UtcNow);
        _factory.VinculoRepositoryMock
            .Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);

        var response = await ClienteTreinador().PatchAsJsonAsync(
            $"/treinador/alunos/{vinculo.Id}/preservar", new { preservar = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DefinirPreservacaoVinculoResponse>();
        body!.PreservarNoLimite.Should().BeFalse();
    }

    [Fact]
    public async Task Patch_Preservar_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().PatchAsJsonAsync(
            $"/treinador/alunos/{Guid.NewGuid()}/preservar", new { preservar = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public class PreservarWebFactory : WebApplicationFactory<Program>
    {
        public Mock<IVinculoTreinadorAlunoRepository> VinculoRepositoryMock { get; } = new();
        public Mock<IUnitOfWork> UnitOfWorkMock { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IVinculoTreinadorAlunoRepository>();
                services.RemoveAll<IUnitOfWork>();
                services.RemoveAll<IUserContext>();

                services.AddScoped(_ => VinculoRepositoryMock.Object);
                services.AddScoped(_ => UnitOfWorkMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.PerfilId).Returns(TreinadorAutenticadoId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.Treinador);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, PreservarTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class PreservarTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public PreservarTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var claims = new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("tipo_conta", "Treinador"),
                new Claim("perfil_id", TreinadorAutenticadoId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
        }
    }
}
