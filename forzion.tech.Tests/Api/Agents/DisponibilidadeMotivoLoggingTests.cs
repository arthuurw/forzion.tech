using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Agents.Disponibilidade;
using forzion.tech.Domain.Entities;
using forzion.tech.Tests.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Api.Agents;

// AGF3-30, segunda cláusula: "Motivo nunca aparece em log". A primeira cláusula (nunca no wire)
// já é provada em ConsultarDisponibilidadeAgenteHandlerTests via reflexão de shape fechado. Este
// teste cobre a cláusula de log capturando TODA saída de ILogger (todo nível, toda categoria)
// durante uma requisição HTTP real de disponibilidade cujo bloqueio tem Motivo definido — não um
// mock pontual de uma única classe, que não pegaria um vazamento vindo de outra camada.
public class DisponibilidadeMotivoLoggingTests
{
    private const string Segredo = "segredo-atual-com-pelo-menos-32-bytes!!";

    [Fact]
    public async Task ConsultaDisponibilidadeComBloqueioComMotivo_MotivoNuncaApareceEmNenhumLog()
    {
        var motivoSentinela = $"MOTIVO-SENTINELA-{Guid.NewGuid():N}";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.WebHost.UseTestServer();
        var capturador = new CapturaTudoLoggerProvider();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(capturador);
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*",
            ["Agents:Hmac:SecretAtual"] = Segredo,
        });
        builder.Services.AddAgentsHmac(builder.Configuration, builder.Environment);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<HmacSignatureVerifier>();
        builder.Services.AddHealthChecks();
        builder.Services.AddAuthentication();
        builder.Services.AddRateLimiter(opt =>
            opt.AddPolicy("agents", _ => RateLimitPartition.GetNoLimiter<string>("test")));

        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Teste", null, null, DateTime.UtcNow);
        treinador.PerfilPublico.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(9, 0), DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);

        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, DateTime.UtcNow);
        pacote.TornarPublico(DateTime.UtcNow);

        var bloqueio = BloqueioAgenda.CriarRecorrente(
            treinador.Id, 1, new TimeOnly(8, 0), new TimeOnly(9, 0), motivoSentinela, DateTime.UtcNow).Value;

        var treinadorRepo = new Mock<ITreinadorRepository>();
        treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        builder.Services.AddSingleton(treinadorRepo.Object);
        var pacoteRepo = new Mock<IPacoteRepository>();
        pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        builder.Services.AddSingleton(pacoteRepo.Object);
        var bloqueioRepo = new Mock<IBloqueioAgendaRepository>();
        bloqueioRepo.Setup(r => r.ListarVigentesAsync(treinador.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BloqueioAgenda>)[bloqueio]);
        builder.Services.AddSingleton(bloqueioRepo.Object);
        var solicitacaoRepo = new Mock<ISolicitacaoAgendamentoRepository>();
        solicitacaoRepo.Setup(r => r.ListarConfirmadasNoIntervaloAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SolicitacaoAgendamento>)[]);
        builder.Services.AddSingleton(solicitacaoRepo.Object);
        builder.Services.AddScoped<ConsultarDisponibilidadeAgenteHandler>();

        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapAgentEndpoints();
        await app.StartAsync();
        using var cliente = app.GetTestClient();

        var caminho = $"{AgentEndpoints.Prefixo}/tenants/{treinador.Id}/availability?serviceId={pacote.Id}&from=2026-08-24T00:00:00Z&to=2026-08-25T00:00:00Z";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"GET\n{caminho}\n{Convert.ToHexStringLower(SHA256.HashData([]))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Segredo), Encoding.UTF8.GetBytes(payload));
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, caminho);
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeAssinatura, "v1=" + Convert.ToHexStringLower(mac));
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeTimestamp, timestamp.ToString(provider: null));

        using var resposta = await cliente.SendAsync(requisicao);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        capturador.Mensagens.Should().NotContain(m => m.Contains(motivoSentinela, StringComparison.Ordinal));
    }

    private sealed class CapturaTudoLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _mensagens = [];
        public IReadOnlyList<string> Mensagens => _mensagens;

        public ILogger CreateLogger(string categoryName) => new CapturaLogger(_mensagens);
        public void Dispose() { }

        private sealed class CapturaLogger(List<string> destino) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var mensagem = formatter(state, exception);
                if (exception is not null)
                    mensagem += " | " + exception;
                lock (destino)
                    destino.Add(mensagem);
            }
        }
    }
}
