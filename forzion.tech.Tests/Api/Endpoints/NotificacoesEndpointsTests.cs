using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
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

public class NotificacoesEndpointsTests : IClassFixture<NotificacoesEndpointsTests.NotificacoesWebFactory>
{
    private readonly NotificacoesWebFactory _factory;
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid OutraContaId = Guid.NewGuid();

    public NotificacoesEndpointsTests(NotificacoesWebFactory factory)
    {
        _factory = factory;
        _factory.Repo.Limpar();
    }

    private HttpClient ClienteAutenticado()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", ContaId.ToString());
        return client;
    }

    private static Notificacao Notif(Guid contaId, TipoNotificacao tipo, DateTime agora, bool lida = false)
    {
        var n = Notificacao.Criar(contaId, tipo, "titulo", "corpo", agora).Value;
        if (lida) n.MarcarLida(agora);
        return n;
    }

    [Fact]
    public async Task Get_Feed_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/notificacoes/");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Feed_RetornaSomenteDaContaOrdenadoDescendente()
    {
        var baseTime = DateTime.UtcNow.AddMinutes(-10);
        var antiga = Notif(ContaId, TipoNotificacao.NovoTreino, baseTime);
        var nova = Notif(ContaId, TipoNotificacao.Reforco, baseTime.AddMinutes(5));
        _factory.Repo.Seed(antiga);
        _factory.Repo.Seed(nova);
        _factory.Repo.Seed(Notif(OutraContaId, TipoNotificacao.NovoTreino, baseTime.AddMinutes(2)));

        var response = await ClienteAutenticado().GetAsync("/notificacoes/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var itens = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = itens.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        ids.Should().Equal(nova.Id, antiga.Id);
    }

    [Fact]
    public async Task Get_Contador_FeedVazio_RetornaZero()
    {
        var response = await ClienteAutenticado().GetAsync("/notificacoes/nao-lidas/contador");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Get_Contador_ContaSomenteNaoLidasDaConta()
    {
        var agora = DateTime.UtcNow;
        _factory.Repo.Seed(Notif(ContaId, TipoNotificacao.NovoTreino, agora));
        _factory.Repo.Seed(Notif(ContaId, TipoNotificacao.Reforco, agora, lida: true));
        _factory.Repo.Seed(Notif(OutraContaId, TipoNotificacao.NovoTreino, agora));

        var response = await ClienteAutenticado().GetAsync("/notificacoes/nao-lidas/contador");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Patch_Lida_Propria_Retorna204EDerrubaContador()
    {
        var agora = DateTime.UtcNow;
        var notif = Notif(ContaId, TipoNotificacao.NovoTreino, agora);
        _factory.Repo.Seed(notif);
        var client = ClienteAutenticado();

        var antes = await (await client.GetAsync("/notificacoes/nao-lidas/contador")).Content.ReadFromJsonAsync<JsonElement>();
        antes.GetProperty("total").GetInt32().Should().Be(1);

        var patch = await client.PatchAsync($"/notificacoes/{notif.Id}/lida", null);

        patch.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var depois = await (await client.GetAsync("/notificacoes/nao-lidas/contador")).Content.ReadFromJsonAsync<JsonElement>();
        depois.GetProperty("total").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Patch_Lida_DeOutraConta_Retorna404ENaoMarca()
    {
        var agora = DateTime.UtcNow;
        var notifOutraConta = Notif(OutraContaId, TipoNotificacao.NovoTreino, agora);
        _factory.Repo.Seed(notifOutraConta);

        var patch = await ClienteAutenticado().PatchAsync($"/notificacoes/{notifOutraConta.Id}/lida", null);

        patch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notifOutraConta.Lida.Should().BeFalse();
    }

    public sealed class NotificacoesWebFactory : WebApplicationFactory<Program>
    {
        public FakeNotificacaoRepository Repo { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<INotificacaoRepository>();
                services.RemoveAll<IUserContext>();

                services.AddScoped<INotificacaoRepository>(_ => Repo);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.ContaId).Returns(ContaId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, NotificacoesTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public sealed class FakeNotificacaoRepository : INotificacaoRepository
    {
        private readonly List<Notificacao> _store = [];

        public void Seed(Notificacao n) => _store.Add(n);
        public void Limpar() => _store.Clear();

        public Task AdicionarAsync(Notificacao notificacao, CancellationToken cancellationToken = default)
        {
            _store.Add(notificacao);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Notificacao>> ListarPorContaAsync(Guid contaId, int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Notificacao>>(
                _store.Where(n => n.DestinatarioContaId == contaId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToList());

        public Task<int> ContarNaoLidasAsync(Guid contaId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.Count(n => n.DestinatarioContaId == contaId && !n.Lida));

        public Task<bool> MarcarLidaAsync(Guid id, Guid contaId, DateTime agora, CancellationToken cancellationToken = default)
        {
            var notif = _store.FirstOrDefault(n => n.Id == id && n.DestinatarioContaId == contaId);
            if (notif is null) return Task.FromResult(false);
            notif.MarcarLida(agora);
            return Task.FromResult(true);
        }

        public Task<int> PurgarAntesDeAsync(DateTime limite, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.RemoveAll(n => n.CreatedAt < limite));
    }

    public sealed class NotificacoesTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public NotificacoesTestAuthHandler(
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
                new Claim("sub", ContaId.ToString()),
                new Claim("tipo_conta", "Aluno")
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
        }
    }
}
