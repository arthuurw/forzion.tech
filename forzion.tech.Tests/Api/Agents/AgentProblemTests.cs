using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace forzion.tech.Tests.Api.Agents;

public class AgentProblemTests
{
    // Enum ErrorCode do contrato `.specs/contracts/forzion-internal-api.v1.yaml`, copiado à mão:
    // reconstruí-lo a partir do próprio enum do código tornaria a asserção tautológica.
    private static readonly string[] CodigosDoContrato =
    [
        "signature_invalid",
        "timestamp_out_of_window",
        "validation_failed",
        "tenant_not_found",
        "service_not_found",
        "slot_not_found",
        "slot_unavailable",
        "idempotency_conflict",
        "dependency_unavailable",
    ];

    private static ServiceProvider ProviderMinimo() =>
        new ServiceCollection().AddLogging().BuildServiceProvider();

    private static ServiceProvider ProviderComCustomizacaoGlobal()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtSecret"] = "segredo-de-teste-com-pelo-menos-32-bytes!!",
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddApiServices(configuracao, Mock.Of<IWebHostEnvironment>(e => e.EnvironmentName == "Test"))
            .BuildServiceProvider();
    }

    private static async Task<(int Status, string? ContentType, JsonElement Corpo)> ExecutarAsync(
        IResult resultado, IServiceProvider servicos)
    {
        var contexto = new DefaultHttpContext { RequestServices = servicos };
        contexto.Response.Body = new MemoryStream();

        await resultado.ExecuteAsync(contexto);

        contexto.Response.Body.Seek(0, SeekOrigin.Begin);
        using var documento = await JsonDocument.ParseAsync(contexto.Response.Body);
        return (contexto.Response.StatusCode, contexto.Response.ContentType, documento.RootElement.Clone());
    }

    [Fact]
    public async Task Criar_EmiteApenasCodigosDoEnumDoContrato()
    {
        using var servicos = ProviderMinimo();

        var emitidos = new List<string>();
        foreach (var code in Enum.GetValues<AgentErrorCode>())
        {
            var (_, _, corpo) = await ExecutarAsync(AgentProblem.Criar(code, StatusCodes.Status400BadRequest), servicos);
            emitidos.Add(corpo.GetProperty("code").GetString()!);
        }

        emitidos.Should().BeEquivalentTo(CodigosDoContrato);
    }

    [Fact]
    public async Task Criar_EmiteOsQuatroCamposObrigatoriosDoSchemaComoProblemJson()
    {
        using var servicos = ProviderMinimo();

        var (status, contentType, corpo) = await ExecutarAsync(
            AgentProblem.Criar(AgentErrorCode.SignatureInvalid, StatusCodes.Status401Unauthorized), servicos);

        status.Should().Be(StatusCodes.Status401Unauthorized);
        contentType.Should().StartWith("application/problem+json");
        corpo.GetProperty("type").GetString().Should().Be("https://forzion.tech/problems/signature_invalid");
        corpo.GetProperty("title").GetString().Should().Be("Signature invalid");
        corpo.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status401Unauthorized);
        corpo.GetProperty("code").GetString().Should().Be("signature_invalid");
    }

    [Fact]
    public async Task Criar_TituloNaoEhSubstituidoPelaCustomizacaoPtBrGlobal()
    {
        using var servicos = ProviderComCustomizacaoGlobal();

        var (_, _, corpo) = await ExecutarAsync(
            AgentProblem.Criar(AgentErrorCode.SignatureInvalid, StatusCodes.Status401Unauthorized), servicos);

        corpo.GetProperty("title").GetString().Should()
            .Be("Signature invalid")
            .And.NotBe(ProblemDetailsTitulos.PtBr[StatusCodes.Status401Unauthorized]);
    }

    [Fact]
    public async Task Criar_DetalheEhLiteralFixoDoCodigoEIgnoraOStatusDoChamador()
    {
        using var servicos = ProviderMinimo();

        var (_, _, quatrocentosUm) = await ExecutarAsync(
            AgentProblem.Criar(AgentErrorCode.TimestampOutOfWindow, StatusCodes.Status401Unauthorized), servicos);
        var (_, _, quinhentosTres) = await ExecutarAsync(
            AgentProblem.Criar(AgentErrorCode.TimestampOutOfWindow, StatusCodes.Status503ServiceUnavailable), servicos);

        quatrocentosUm.GetProperty("detail").GetString().Should()
            .Be("The request timestamp is outside the accepted window.");
        quinhentosTres.GetProperty("detail").GetString().Should()
            .Be(quatrocentosUm.GetProperty("detail").GetString());
    }
}
