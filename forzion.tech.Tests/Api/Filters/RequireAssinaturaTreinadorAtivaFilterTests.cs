using FluentAssertions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace forzion.tech.Tests.Api.Filters;

public class RequireAssinaturaTreinadorAtivaFilterTests
{
    private static (EndpointFilterInvocationContext ctx, RequireAssinaturaTreinadorAtivaFilter filter, EndpointFilterDelegate next, Action<bool> assertNextCalled)
        Cenario(
            TipoConta tipoConta,
            string method = "POST",
            AssinaturaTreinador? assinatura = null)
    {
        var treinadorId = Guid.NewGuid();

        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.TipoConta).Returns(tipoConta);
        userContext.SetupGet(u => u.PerfilId).Returns(treinadorId);

        var assinaturaRepository = new Mock<IAssinaturaTreinadorRepository>();
        assinaturaRepository
            .Setup(r => r.ObterAtualPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var services = new ServiceCollection();
        services.AddSingleton(userContext.Object);
        services.AddSingleton(assinaturaRepository.Object);
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

    private static AssinaturaTreinador CriarAssinatura(Guid treinadorId, AssinaturaTreinadorStatus status)
    {
        var agora = DateTime.UtcNow;
        var a = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 100m, agora).Value;
        switch (status)
        {
            case AssinaturaTreinadorStatus.Ativa:
                a.Ativar(agora);
                break;
            case AssinaturaTreinadorStatus.Inadimplente:
                a.Ativar(agora);
                a.MarcarInadimplente(agora);
                break;
            case AssinaturaTreinadorStatus.Cancelada:
                a.Cancelar(agora);
                break;
        }
        return a;
    }

    [Fact]
    public async Task Get_TreinadorInadimplente_PassaDireto()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "GET");

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Post_TreinadorInadimplente_Retorna403ComCode()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = CriarAssinatura(treinadorId, AssinaturaTreinadorStatus.Inadimplente);

        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.TipoConta).Returns(TipoConta.Treinador);
        userContext.SetupGet(u => u.PerfilId).Returns(treinadorId);

        var assinaturaRepository = new Mock<IAssinaturaTreinadorRepository>();
        assinaturaRepository
            .Setup(r => r.ObterAtualPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var services = new ServiceCollection();
        services.AddSingleton(userContext.Object);
        services.AddSingleton(assinaturaRepository.Object);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Request.Method = "POST";
        var ctx = new DefaultEndpointFilterInvocationContext(httpContext);

        var called = false;
        EndpointFilterDelegate next = _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        var filter = new RequireAssinaturaTreinadorAtivaFilter();
        var result = await filter.InvokeAsync(ctx, next);

        called.Should().BeFalse();
        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        problem.ProblemDetails.Title.Should().Be("Assinatura inadimplente");
        problem.ProblemDetails.Extensions.Should().ContainKey("code");
        problem.ProblemDetails.Extensions["code"].Should().Be("ASSINATURA_TREINADOR_INADIMPLENTE");
    }

    [Fact]
    public async Task Put_TreinadorInadimplente_Retorna403()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = CriarAssinatura(treinadorId, AssinaturaTreinadorStatus.Inadimplente);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "PUT", assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(false);
        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Delete_TreinadorInadimplente_Retorna403()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = CriarAssinatura(treinadorId, AssinaturaTreinadorStatus.Inadimplente);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "DELETE", assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(false);
        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Post_TreinadorComAssinaturaAtiva_ChamaNext()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = CriarAssinatura(treinadorId, AssinaturaTreinadorStatus.Ativa);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "POST", assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(true);
        result.Should().BeAssignableTo<IResult>();
    }

    [Fact]
    public async Task Post_TreinadorComAssinaturaPendente_ChamaNext()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = CriarAssinatura(treinadorId, AssinaturaTreinadorStatus.Pendente);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "POST", assinatura);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Post_TreinadorComAssinaturaCancelada_ChamaNext()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = CriarAssinatura(treinadorId, AssinaturaTreinadorStatus.Cancelada);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "POST", assinatura);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Post_TreinadorSemAssinatura_ChamaNext()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "POST", assinatura: null);

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
    public async Task Post_TipoNaoTreinador_NaoConsultaRepositorio()
    {
        var assinaturaRepoMock = new Mock<IAssinaturaTreinadorRepository>(MockBehavior.Strict);

        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.TipoConta).Returns(TipoConta.Aluno);
        userContext.SetupGet(u => u.PerfilId).Returns(Guid.NewGuid());

        var services = new ServiceCollection();
        services.AddSingleton(userContext.Object);
        services.AddSingleton(assinaturaRepoMock.Object);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Request.Method = "POST";
        var ctx = new DefaultEndpointFilterInvocationContext(httpContext);

        var called = false;
        EndpointFilterDelegate next = _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        var filter = new RequireAssinaturaTreinadorAtivaFilter();
        await filter.InvokeAsync(ctx, next);

        called.Should().BeTrue();
        assinaturaRepoMock.VerifyNoOtherCalls();
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
