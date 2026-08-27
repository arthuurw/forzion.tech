using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

// AGF3-13 / risco R2 do design: prova que falha do repositório durante a consulta de disponibilidade
// vira 503 dependency_unavailable — nunca 200 com lista vazia. Reusa o mecanismo já existente do
// AgentExceptionFilter (D-J); esta é a regressão que protege a invariante mais cara da fatia.
public class DisponibilidadeFalhaTests
{
    private const string Segredo = "segredo-atual-com-pelo-menos-32-bytes!!";

    [Fact]
    public async Task RepositorioLancaDuranteConsulta_Retorna503ComDependencyUnavailableENuncaListaVazia()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
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

        var treinadorRepoQueLanca = new Mock<ITreinadorRepository>();
        treinadorRepoQueLanca
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("timeout de conexao com o postgres"));
        builder.Services.AddSingleton(treinadorRepoQueLanca.Object);
        builder.Services.AddSingleton(new Mock<IPacoteRepository>().Object);
        builder.Services.AddSingleton(new Mock<IBloqueioAgendaRepository>().Object);
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

        var caminho = $"{AgentEndpoints.Prefixo}/tenants/{Guid.NewGuid()}/availability?serviceId={Guid.NewGuid()}&from=2026-08-24T00:00:00Z&to=2026-08-25T00:00:00Z";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"GET\n{caminho}\n{Convert.ToHexStringLower(SHA256.HashData([]))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Segredo), Encoding.UTF8.GetBytes(payload));
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, caminho);
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeAssinatura, "v1=" + Convert.ToHexStringLower(mac));
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeTimestamp, timestamp.ToString(provider: null));

        using var resposta = await cliente.SendAsync(requisicao);

        resposta.StatusCode.Should().NotBe(HttpStatusCode.OK);
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var texto = await resposta.Content.ReadAsStringAsync();
        using var corpo = JsonDocument.Parse(texto);
        corpo.RootElement.ValueKind.Should().NotBe(JsonValueKind.Array, "a resposta de falha nunca pode ter o shape de uma lista de slots vazia");
        corpo.RootElement.GetProperty("code").GetString().Should().Be("dependency_unavailable");
        corpo.RootElement.TryGetProperty("slotId", out _).Should().BeFalse("o corpo do erro não pode conter dado de domínio");
    }

    // AGF3-13.3: um fuso que era válido na escrita (validado por Treinador.DefinirFusoHorario) mas
    // não resolve mais no runtime de leitura (ex.: dado legado, tzdata do SO divergente) precisa
    // virar 503 pelo mesmo caminho — nunca 200 []. O setter privado de FusoHorario não permite
    // simular esse drift pela API pública da entidade, daí a escrita via reflexão.
    [Fact]
    public async Task FusoHorarioPersistidoNaoResolveNoRuntime_Retorna503ComDependencyUnavailableENuncaListaVazia()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
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
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        typeof(Treinador).GetProperty(nameof(Treinador.FusoHorario))!.SetValue(treinador, "Nao/Existe_Nesta_Tzdata");

        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, DateTime.UtcNow);
        pacote.TornarPublico(DateTime.UtcNow);

        var treinadorRepo = new Mock<ITreinadorRepository>();
        treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        builder.Services.AddSingleton(treinadorRepo.Object);
        var pacoteRepo = new Mock<IPacoteRepository>();
        pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        builder.Services.AddSingleton(pacoteRepo.Object);
        builder.Services.AddSingleton(new Mock<IBloqueioAgendaRepository>().Object);
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

        resposta.StatusCode.Should().NotBe(HttpStatusCode.OK);
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var texto = await resposta.Content.ReadAsStringAsync();
        using var corpo = JsonDocument.Parse(texto);
        corpo.RootElement.ValueKind.Should().NotBe(JsonValueKind.Array, "um fuso que nao resolve nunca pode virar lista vazia de slots");
        corpo.RootElement.GetProperty("code").GetString().Should().Be("dependency_unavailable");
    }
}
