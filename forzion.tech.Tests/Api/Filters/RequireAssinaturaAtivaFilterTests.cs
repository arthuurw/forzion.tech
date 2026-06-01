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
    private static readonly DateTime Agora = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static (EndpointFilterInvocationContext ctx, RequireAssinaturaAtivaFilter filter, EndpointFilterDelegate next, Action<bool> assertNextCalled)
        Cenario(
            TipoConta tipoConta,
            Aluno? aluno = null,
            AssinaturaAluno? assinatura = null)
    {
        var contaId = Guid.NewGuid();
        var perfilId = aluno?.Id ?? Guid.NewGuid();

        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(u => u.TipoConta).Returns(tipoConta);
        userContext.SetupGet(u => u.ContaId).Returns(contaId);
        userContext.SetupGet(u => u.PerfilId).Returns(perfilId);

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
        var ctx = new DefaultEndpointFilterInvocationContext(httpContext);

        var called = false;
        EndpointFilterDelegate next = _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        return (ctx, new RequireAssinaturaAtivaFilter(), next, expected => called.Should().Be(expected));
    }

    private static AssinaturaAluno CriarAssinaturaComStatus(Guid alunoId, AssinaturaAlunoStatus status)
    {
        var assinatura = AssinaturaAluno.Criar(
            vinculoId: Guid.NewGuid(),
            pacoteId: Guid.NewGuid(),
            treinadorId: Guid.NewGuid(),
            alunoId: alunoId,
            valor: 100m,
            agora: Agora).Value;

        switch (status)
        {
            case AssinaturaAlunoStatus.Pendente:
                break;
            case AssinaturaAlunoStatus.Ativa:
                assinatura.Ativar(Agora);
                break;
            case AssinaturaAlunoStatus.Inadimplente:
                assinatura.Ativar(Agora);
                assinatura.MarcarInadimplente(Agora);
                break;
            case AssinaturaAlunoStatus.Cancelada:
                assinatura.Cancelar(Agora);
                break;
        }

        return assinatura;
    }

    private static Aluno CriarAluno() =>
        Aluno.Criar(Guid.NewGuid(), "Aluno Teste", Agora).Value;

    [Fact]
    public async Task Aluno_ComAssinaturaAtiva_ChamaNext()
    {
        var aluno = CriarAluno();
        var assinatura = CriarAssinaturaComStatus(aluno.Id, AssinaturaAlunoStatus.Ativa);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, aluno, assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(true);
        result.Should().BeAssignableTo<IResult>();
    }

    [Fact]
    public async Task Aluno_ComAssinaturaInadimplente_Retorna403ComCode()
    {
        var aluno = CriarAluno();
        var assinatura = CriarAssinaturaComStatus(aluno.Id, AssinaturaAlunoStatus.Inadimplente);
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, aluno, assinatura);

        var result = await filter.InvokeAsync(ctx, next);

        assertCalled(false);
        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        problem.ProblemDetails.Title.Should().Be("Assinatura inadimplente");
        problem.ProblemDetails.Extensions.Should().ContainKey("code");
        problem.ProblemDetails.Extensions["code"].Should().Be("ASSINATURA_INADIMPLENTE");
    }

    [Fact]
    public async Task Aluno_ComAssinaturaCancelada_BypassaENaoBloqueia()
    {
        var aluno = CriarAluno();
        // ObterAtualPorAlunoAsync exclui Cancelada — simula retornando null
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, aluno, assinatura: null);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Aluno_SemAssinatura_Bypassa()
    {
        var aluno = CriarAluno();
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, aluno, assinatura: null);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Aluno_SemRegistroDeAluno_Bypassa()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Aluno, aluno: null);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Treinador_Bypassa()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.Treinador);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task SystemAdmin_Bypassa()
    {
        var (ctx, filter, next, assertCalled) = Cenario(TipoConta.SystemAdmin);

        await filter.InvokeAsync(ctx, next);

        assertCalled(true);
    }

    [Fact]
    public async Task Aluno_AssinaturaAtiva_NaoConsultaQuandoTipoNaoEhAluno()
    {
        // Garante que para tipo != Aluno o filtro nem consulta repositórios
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
