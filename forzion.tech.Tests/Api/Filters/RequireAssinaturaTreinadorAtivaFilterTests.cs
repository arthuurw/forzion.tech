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
    private static readonly DateTime Agora = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static (EndpointFilterInvocationContext ctx, RequireAssinaturaTreinadorAtivaFilter filter, EndpointFilterDelegate next, Action<bool> assertNextCalled)
        Cenario(
            TipoConta tipoConta,
            Guid? perfilId = null,
            AssinaturaTreinador? assinatura = null)
    {
        var treinadorId = perfilId ?? Guid.NewGuid();

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
        var ctx = new DefaultEndpointFilterInvocationContext(httpContext);

        var called = false;
        EndpointFilterDelegate next = _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        return (ctx, new RequireAssinaturaTreinadorAtivaFilter(), next, expected => called.Should().Be(expected));
    }

    private static AssinaturaTreinador CriarAssinaturaComStatus(Guid treinadorId, AssinaturaTreinadorStatus status)
    {
        var a = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 100m, Agora).Value;
        switch (status)
        {
            case AssinaturaTreinadorStatus.Ativa:
                a.Ativar(Agora);
                break;
            case AssinaturaTreinadorStatus.Inadimplente:
                a.Ativar(Agora);
                a.MarcarInadimplente(Agora);
                break;
            case AssinaturaTreinadorStatus.Cancelada:
                a.Cancelar(Agora);
                break;
        }
        return a;
    }

    [Fact]
    public async Task Treinador_ComAssinaturaAtiva_ChamaNext()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = CriarAssinaturaComStatus(treinadorId, AssinaturaTreinadorStatus.Ativa);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, treinadorId, assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(true);
        result.Should().BeAssignableTo<IResult>();
    }

    [Fact]
    public async Task Treinador_ComAssinaturaInadimplente_Retorna403ComCode()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = CriarAssinaturaComStatus(treinadorId, AssinaturaTreinadorStatus.Inadimplente);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, treinadorId, assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(false);
        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        problem.ProblemDetails.Title.Should().Be("Assinatura inadimplente");
        problem.ProblemDetails.Extensions.Should().ContainKey("code");
        problem.ProblemDetails.Extensions["code"].Should().Be("ASSINATURA_TREINADOR_INADIMPLENTE");
    }

    [Fact]
    public async Task Treinador_SemAssinatura_ChamaNext()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, assinatura: null);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Treinador_AssinaturaPendente_ChamaNext()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = CriarAssinaturaComStatus(treinadorId, AssinaturaTreinadorStatus.Pendente);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, treinadorId, assinatura);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Aluno_Bypassa_SemConsultarAssinaturaTreinador()
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
    public async Task SystemAdmin_Bypassa()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.SystemAdmin);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }
}
