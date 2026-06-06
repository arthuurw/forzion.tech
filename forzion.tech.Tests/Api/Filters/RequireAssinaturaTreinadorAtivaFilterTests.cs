using FluentAssertions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace forzion.tech.Tests.Api.Filters;

public class RequireAssinaturaTreinadorAtivaFilterTests
{
    private static (EndpointFilterInvocationContext ctx, RequireAssinaturaTreinadorAtivaFilter filter, EndpointFilterDelegate next, Action<bool> assertNextCalled)
        Cenario(TipoConta tipoConta, string method = "POST")
    {
        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.TipoConta).Returns(tipoConta);
        userContext.SetupGet(u => u.PerfilId).Returns(Guid.NewGuid());

        var services = new ServiceCollection();
        services.AddSingleton(userContext.Object);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Request.Method = method;
        var ctx = new DefaultEndpointFilterInvocationContext(httpContext);

        var called = false;
        EndpointFilterDelegate next = _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        return (ctx, new RequireAssinaturaTreinadorAtivaFilter(), next, expected => called.Should().Be(expected));
    }

    [Fact]
    public async Task Get_TreinadorInadimplente_PassaDireto()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "GET");

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Post_Treinador_ChamaNext()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "POST");

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Put_Treinador_ChamaNext()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "PUT");

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Delete_Treinador_ChamaNext()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "DELETE");

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Post_Aluno_Bypassa()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, "POST");

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Post_SystemAdmin_Bypassa()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.SystemAdmin, "POST");

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task CodigoErro_EhAssinaturaTreinadorInadimplente()
    {
        var filter = new RequireAssinaturaTreinadorAtivaFilter();
        var codigoErroAluno = new RequireAssinaturaAtivaFilter();

        var propTreinador = typeof(RequireAssinaturaTreinadorAtivaFilter)
            .GetProperty("CodigoErro",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var propAluno = typeof(RequireAssinaturaAtivaFilter)
            .GetProperty("CodigoErro",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var codTreinador = (string?)propTreinador!.GetValue(filter);
        var codAluno = (string?)propAluno!.GetValue(codigoErroAluno);

        codTreinador.Should().Be("ASSINATURA_TREINADOR_INADIMPLENTE");
        codAluno.Should().Be("ASSINATURA_INADIMPLENTE");
        codTreinador.Should().NotBe(codAluno);
    }
}
