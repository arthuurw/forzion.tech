using FluentAssertions;
using forzion.tech.Api.Endpoints.Agents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Xunit;

namespace forzion.tech.Tests.Api.Agents;

public class AgentExceptionFilterTests
{
    private sealed class LoggerFake : ILogger<AgentExceptionFilter>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entradas { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entradas.Add((logLevel, exception, formatter(state, exception)));
    }

    private static EndpointFilterInvocationContext CriarContexto()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/internal/agents/v1/tenants/x/business-info";

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    [Fact]
    public async Task ExcecaoNaoTratada_RespondeQuinhentosETresComDependencyUnavailable()
    {
        var filter = new AgentExceptionFilter(new LoggerFake());
        EndpointFilterDelegate next = _ => throw new InvalidOperationException("timeout de conexao com o postgres");

        var resultado = await filter.InvokeAsync(CriarContexto(), next);

        var problem = resultado.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        problem.ProblemDetails.Extensions["code"].Should().Be("dependency_unavailable");
    }

    [Fact]
    public async Task ExcecaoNaoTratada_NuncaVazaMensagemDaExcecaoNoCorpo()
    {
        var filter = new AgentExceptionFilter(new LoggerFake());
        const string MensagemSensivel = "timeout de conexao com o postgres do tenant 7f3a";
        EndpointFilterDelegate next = _ => throw new InvalidOperationException(MensagemSensivel);

        var resultado = await filter.InvokeAsync(CriarContexto(), next);

        var problem = resultado.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.ProblemDetails.Detail.Should().NotContain(MensagemSensivel);
        problem.ProblemDetails.Title.Should().NotContain(MensagemSensivel);
    }

    [Fact]
    public async Task ExcecaoNaoTratada_LogaAExcecaoRealComNivelError()
    {
        var logger = new LoggerFake();
        var filter = new AgentExceptionFilter(logger);
        var excecao = new InvalidOperationException("timeout de conexao com o postgres");
        EndpointFilterDelegate next = _ => throw excecao;

        await filter.InvokeAsync(CriarContexto(), next);

        logger.Entradas.Should().ContainSingle(e => e.Level == LogLevel.Error && ReferenceEquals(e.Exception, excecao));
    }

    [Fact]
    public async Task SemExcecao_RepassaORetornoDoProximoDelegateSemAlterar()
    {
        var filter = new AgentExceptionFilter(new LoggerFake());
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(Results.Ok());

        var resultado = await filter.InvokeAsync(CriarContexto(), next);

        resultado.Should().BeOfType<Ok>();
    }
}
