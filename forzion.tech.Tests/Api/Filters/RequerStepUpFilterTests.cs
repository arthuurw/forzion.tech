using FluentAssertions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Auth;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace forzion.tech.Tests.Api.Filters;

public class RequerStepUpFilterTests
{
    private sealed class Cenario
    {
        public required EndpointFilterInvocationContext Ctx { get; init; }
        public required EndpointFilterDelegate Next { get; init; }
        public required Func<bool> NextCalled { get; init; }
    }

    private static Cenario Construir(
        Guid contaUsuario,
        string? header,
        EscopoValidado? validado,
        bool revogado = false)
    {
        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.ContaId).Returns(contaUsuario);

        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.ValidarTokenEscopo(It.IsAny<string>(), MfaScopes.StepUp)).Returns(validado);

        var revogados = new Mock<ITokenRevogadoRepository>();
        revogados.Setup(r => r.EstaRevogadoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(revogado);

        var services = new ServiceCollection();
        services.AddSingleton(userContext.Object);
        services.AddSingleton(jwt.Object);
        services.AddSingleton(revogados.Object);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Request.Method = "POST";
        if (header is not null)
            httpContext.Request.Headers[RequerStepUpFilter.Header] = header;

        var ctx = new DefaultEndpointFilterInvocationContext(httpContext);
        var called = false;
        EndpointFilterDelegate next = _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        return new Cenario { Ctx = ctx, Next = next, NextCalled = () => called };
    }

    private static void DeveSerStepUpRequerido(object? result, Func<bool> nextCalled)
    {
        nextCalled().Should().BeFalse();
        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        problem.ProblemDetails.Extensions["code"].Should().Be(RequerStepUpFilter.CodigoErro);
    }

    [Fact]
    public async Task SemHeader_RetornaStepUpRequerido()
    {
        var c = Construir(Guid.NewGuid(), header: null, validado: null);

        var result = await new RequerStepUpFilter().InvokeAsync(c.Ctx, c.Next);

        DeveSerStepUpRequerido(result, c.NextCalled);
    }

    [Fact]
    public async Task TokenInvalido_RetornaStepUpRequerido()
    {
        var c = Construir(Guid.NewGuid(), header: "lixo", validado: null);

        var result = await new RequerStepUpFilter().InvokeAsync(c.Ctx, c.Next);

        DeveSerStepUpRequerido(result, c.NextCalled);
    }

    [Fact]
    public async Task TokenDeOutraConta_RetornaStepUpRequerido()
    {
        var conta = Guid.NewGuid();
        var c = Construir(conta, header: "tok", validado: new EscopoValidado(Guid.NewGuid(), Guid.NewGuid()));

        var result = await new RequerStepUpFilter().InvokeAsync(c.Ctx, c.Next);

        DeveSerStepUpRequerido(result, c.NextCalled);
    }

    [Fact]
    public async Task TokenRevogado_RetornaStepUpRequerido()
    {
        var conta = Guid.NewGuid();
        var c = Construir(conta, header: "tok", validado: new EscopoValidado(conta, Guid.NewGuid()), revogado: true);

        var result = await new RequerStepUpFilter().InvokeAsync(c.Ctx, c.Next);

        DeveSerStepUpRequerido(result, c.NextCalled);
    }

    [Fact]
    public async Task TokenValidoDaConta_ChamaNext()
    {
        var conta = Guid.NewGuid();
        var c = Construir(conta, header: "tok", validado: new EscopoValidado(conta, Guid.NewGuid()));

        await new RequerStepUpFilter().InvokeAsync(c.Ctx, c.Next);

        c.NextCalled().Should().BeTrue();
    }
}
