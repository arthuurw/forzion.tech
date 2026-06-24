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

public class RequireAssinaturaAtivaFilterTests
{
    private static (EndpointFilterInvocationContext ctx, RequireAssinaturaAtivaFilter filter, EndpointFilterDelegate next, Action<bool> assertNextCalled)
        Cenario(
            TipoConta tipoConta,
            string method = "POST",
            Aluno? aluno = null,
            AssinaturaAluno? assinatura = null)
    {
        var contaId = Guid.NewGuid();

        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.TipoConta).Returns(tipoConta);
        userContext.SetupGet(u => u.ContaId).Returns(contaId);
        userContext.SetupGet(u => u.PerfilId).Returns(aluno?.Id ?? Guid.NewGuid());

        var alunoRepository = new Mock<IAlunoRepository>();
        alunoRepository
            .Setup(r => r.ObterPorContaIdAsync(contaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        var assinaturaRepository = new Mock<IAssinaturaAlunoRepository>();
        if (aluno is not null)
        {
            assinaturaRepository
                .Setup(r => r.ObterAtualPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(assinatura);
        }

        var services = new ServiceCollection();
        services.AddSingleton(userContext.Object);
        services.AddSingleton(alunoRepository.Object);
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

        return (ctx, new RequireAssinaturaAtivaFilter(), next, expected => called.Should().Be(expected));
    }

    private static Aluno CriarAluno() =>
        Aluno.Criar(Guid.NewGuid(), "Aluno Teste", DateTime.UtcNow).Value;

    private static AssinaturaAluno CriarAssinatura(Guid alunoId, AssinaturaAlunoStatus status)
    {
        var agora = DateTime.UtcNow;
        var a = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), alunoId, 100m, agora).Value;
        switch (status)
        {
            case AssinaturaAlunoStatus.Ativa:
                a.Ativar(agora);
                break;
            case AssinaturaAlunoStatus.Inadimplente:
                a.Ativar(agora);
                a.MarcarInadimplente(agora);
                break;
            case AssinaturaAlunoStatus.Cancelada:
                a.Cancelar(agora);
                break;
        }
        return a;
    }

    [Fact]
    public async Task Get_AlunoInadimplente_PassaDireto()
    {
        var aluno = CriarAluno();
        var assinatura = CriarAssinatura(aluno.Id, AssinaturaAlunoStatus.Inadimplente);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, "GET", aluno, assinatura);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Post_AlunoInadimplente_Retorna403ComCode()
    {
        var aluno = CriarAluno();
        var assinatura = CriarAssinatura(aluno.Id, AssinaturaAlunoStatus.Inadimplente);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, "POST", aluno, assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(false);
        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        problem.ProblemDetails.Title.Should().Be("Assinatura inadimplente");
        problem.ProblemDetails.Extensions.Should().ContainKey("code");
        problem.ProblemDetails.Extensions["code"].Should().Be("ASSINATURA_INADIMPLENTE");
    }

    [Fact]
    public async Task Put_AlunoInadimplente_Retorna403()
    {
        var aluno = CriarAluno();
        var assinatura = CriarAssinatura(aluno.Id, AssinaturaAlunoStatus.Inadimplente);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, "PUT", aluno, assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(false);
        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Delete_AlunoInadimplente_Retorna403()
    {
        var aluno = CriarAluno();
        var assinatura = CriarAssinatura(aluno.Id, AssinaturaAlunoStatus.Inadimplente);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, "DELETE", aluno, assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(false);
        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Post_AlunoComAssinaturaAtiva_ChamaNext()
    {
        var aluno = CriarAluno();
        var assinatura = CriarAssinatura(aluno.Id, AssinaturaAlunoStatus.Ativa);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, "POST", aluno, assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(true);
        result.Should().BeAssignableTo<IResult>();
    }

    [Fact]
    public async Task Post_AlunoSemAssinatura_Bypassa()
    {
        var aluno = CriarAluno();
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, "POST", aluno, assinatura: null);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Post_AlunoNaoRegistrado_Bypassa()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, "POST", aluno: null);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Post_Treinador_Bypassa()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador, "POST");

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
    public async Task Post_TipoNaoAluno_NaoConsultaRepositorios()
    {
        var alunoRepoMock = new Mock<IAlunoRepository>(MockBehavior.Strict);
        var assinaturaRepoMock = new Mock<IAssinaturaAlunoRepository>(MockBehavior.Strict);

        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.TipoConta).Returns(TipoConta.Treinador);
        userContext.SetupGet(u => u.ContaId).Returns(Guid.NewGuid());

        var services = new ServiceCollection();
        services.AddSingleton(userContext.Object);
        services.AddSingleton(alunoRepoMock.Object);
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

        var filter = new RequireAssinaturaAtivaFilter();
        await filter.InvokeAsync(ctx, next);

        called.Should().BeTrue();
        alunoRepoMock.VerifyNoOtherCalls();
        assinaturaRepoMock.VerifyNoOtherCalls();
    }
}
